using AccuSchedule.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AccuSchedule.UI.Extensions;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MoreLinq;
using System.Windows.Media.TextFormatting;
using ClosedXML.Report.Utils;
using System.Web.UI.WebControls.WebParts;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class BuildPaperWorkTool : ToolPlugin
    {
        const string title = "Accuplan";

        public override string DefaultSection { get => title; } // Plugin Name (Header used when no type is filtered)
        public override Type[] TypesToLoad => new Type[] { typeof(DataSet), typeof(DataTable) }; // Only load methods with these return types, blank will show all
        public override string NameOfSection(Type methodType) // Name of sections according to return type
        {
            if (methodType is IEnumerable<object>
                || methodType == typeof(DataTable)
                || methodType == typeof(DataSet)) return title;

            return string.Empty;
        }

        private static bool PayLoadIsSavable { get; } = false;


        public DataSet BuildPaperWork(IEnumerable<object> ConnectedNodeObjs, Action<IEnumerable<object>> Build, string MaxQtyPerTraveler)
        {
            foreach (var obj in ConnectedNodeObjs)
                if (obj.GetObjectName().Contains("Schedule"))
                    TablePrefix = obj.GetObjectName().Replace("Schedule", "");
                
            
            

            if (ConnectedNodeObjs.Any())
            {
                // Create WO|Es Table
                CreateWOsTable();
                CreateWOPartsTable();
                CreateWOModelsTable();
                // Create Bus Kits table
                CreateBusKitsTable();
                CreateBusKitPartsTable();
                CreateBusKitPartMatsTable();
                // Create summary tables
                CreateVinylOrderTable();
                CreateCutSummaryTable();
                CreateTravelerSummaryTable();
                CreateLaborsSummaryTable();
                CreateLaborColumnNamesTable();
                CreateLaborTotalsTable();

                // Populate the tables
                PopulateWOsSet(ConnectedNodeObjs, Convert.ToInt32(MaxQtyPerTraveler));
                PopulateBusKitSet(ConnectedNodeObjs);
                PopulateVinylOrders(ConnectedNodeObjs);
                PopulateCutSummary(ConnectedNodeObjs);
                PopulateTravelerSummary(ConnectedNodeObjs);

                // Redo Parent PN on cut tickets and description
                RePopulateCutPNandDesc(ConnectedNodeObjs);

                // Finalize the Dataset
                SetupDataSet();
                SetupRelationships();
            }

            return tablesSet;
        }

        private (DataTable Travelers, DataTable Parts) RenumberTravelerIDsByMaxQty(int maxQty)
        {

            // Get a list of all the items needing renamed
            var renumberedTravelers = WOsTable.Clone();
            var renumberedTravelerParts = WOParts.Clone();


            foreach (var traveler in WOsTable.AsEnumerable())
            {
                var travelerID = traveler.Field<object>("TravelerID").ToString();
                var travelerParts = WOParts.AsEnumerable()
                    .Where(w => w.Field<object>("TravelerID").ToString() == travelerID)
                    .OrderBy(o => o.Field<object>("Qty").ToString())
                    .ToList();
                var travelerPartQty = travelerParts.Sum(s => Convert.ToInt32(s.Field<object>("Qty").ToString()));

                // Split if greater than max quantity
                if (travelerPartQty > maxQty)
                {
                    var qtyRemaining = maxQty;
                    var combineToMax = new HashSet<(DataRow Row, int ID, int Qty, string dateRequested)>(); // Part Row and the Int of the split Traveler with new qty
                    var dateRequested = traveler.Field<object>("DateRequested")?.ToString();

                    var newTravelerNum = 1;

                    // Combine small quantities
                    foreach (var part in travelerParts.Where(w => Convert.ToInt32(w.Field<object>("Qty").ToString())<= maxQty))
                    {
                        var partQty = Convert.ToInt32(part.Field<object>("Qty").ToString());
                        if (partQty <= qtyRemaining)
                        { // Add for combining
                            qtyRemaining -= partQty;
                            combineToMax.Add((part, newTravelerNum, partQty, dateRequested));
                        }
                        else if (partQty > qtyRemaining && combineToMax.Any())
                        { // Increment TravelerID

                            newTravelerNum += 1;
                            qtyRemaining = maxQty;
                            combineToMax.Add((part, newTravelerNum, partQty, dateRequested));

                        }
                    }

                    // Split large quantities
                    foreach (var part in travelerParts.Where(w => Convert.ToInt32(w.Field<object>("Qty").ToString()) > maxQty))
                    {
                        var partQty = Convert.ToInt32(part.Field<object>("Qty").ToString());

                        do
                        {
                            if (partQty - maxQty <= 0)
                            { // Add remaining qty to it's own Traveler
                                newTravelerNum += 1;
                                combineToMax.Add((part, newTravelerNum, partQty, dateRequested));
                                partQty = 0;
                            }
                            else
                            { // Split the quantity by max
                                newTravelerNum += 1;
                                combineToMax.Add((part, newTravelerNum, maxQty, dateRequested));
                                partQty -= maxQty;
                            }
                        } while (partQty > 0);

                    }

                    // Add rows from 'combineToMax' with the new TravelerID
                    foreach (var part in combineToMax)
                    {
                        var newTravelerID = travelerID + "_" + part.ID;

                        var totalTravelerQty = combineToMax.Where(w => w.ID == part.ID).Sum(add => add.Qty);

                        var newPartRow = renumberedTravelerParts.NewRow();
                        newPartRow.SetField("TravelerID", newTravelerID);
                        newPartRow.SetField("PN", part.Row.Field<object>("PN")?.ToString());
                        newPartRow.SetField("Desc1", part.Row.Field<object>("Desc1")?.ToString());
                        newPartRow.SetField("Parent", part.Row.Field<object>("Parent")?.ToString());
                        newPartRow.SetField("Qty", part.Qty);
                        renumberedTravelerParts.Rows.Add(newPartRow);

                        var newTravelerRow = renumberedTravelers.NewRow();
                        newTravelerRow.SetField("TravelerID", newTravelerID);
                        newTravelerRow.SetField("DateRequested", part.dateRequested);
                        newTravelerRow.SetField("PartsOnTraveler", totalTravelerQty);
                        renumberedTravelers.Rows.Add(newTravelerRow);
                    }
                }

            }

            return (renumberedTravelers, renumberedTravelerParts);

        }


        private void SetupRelationships()
        {
            // Set up WOs Relationships
            tablesSet.Relations.Add("Parts", WOsTable.Columns["TravelerID"], WOParts.Columns["TravelerID"]);
            //tablesSet.Relations.Add("Models",
            //     new DataColumn[] { WOParts.Columns["TravelerID"], WOParts.Columns["PN"] },
            //     new DataColumn[] { WOModels.Columns["TravelerID"], WOModels.Columns["PN"] });

            // Set up Bus Kit Relationships
            tablesSet.Relations.Add("KitParts", BusKitsTable.Columns["BusID"], BusKitPartsTable.Columns["BusID"]);
            //BusKitSet.Relations.Add("PartChildren", BusKitPartsTable.Columns["PN"], BusKitPartChildrenTable.Columns["Parent"]);

            tablesSet.Relations.Add("PartMaterials",
                new DataColumn[] { BusKitPartsTable.Columns["OrderNumbers"], BusKitPartsTable.Columns["BusID"], BusKitPartsTable.Columns["PN"] },
                new DataColumn[] { BusKitPartMatsTable.Columns["OrderNumbers"], BusKitPartMatsTable.Columns["BusID"], BusKitPartMatsTable.Columns["Parent"] });
        }
        private void SetupDataSet()
        {
            tablesSet = new DataSet() { DataSetName = TablePrefix + "PaperWorkSet" };

            // WO Tables
            tablesSet.Tables.Add(WOsTable);
            tablesSet.Tables.Add(WOParts);
            
            // Bus Kit Tables
            tablesSet.Tables.Add(BusKitsTable);
            tablesSet.Tables.Add(BusKitPartsTable);
            tablesSet.Tables.Add(BusKitPartMatsTable);

            // Vinyl Order
            tablesSet.Tables.Add(VinylOrders);

            // Cut Summary
            tablesSet.Tables.Add(CutSummary);

            // Traveler Summary
            tablesSet.Tables.Add(TravelerSummary);
            tablesSet.Tables.Add(LaborColumns);
            tablesSet.Tables.Add(LaborTotals);
        }


        private DataSet tablesSet { get; set; }

        private string TablePrefix { get; set; }


        #region PaperWork Functionality

        #region WO Set
        private DataTable WOsTable { get; set; }
        private DataTable WOParts { get; set; }
        private DataTable WOModels { get; set; }

        private void CreateWOsTable()
        {
            WOsTable = new DataTable() { TableName = TablePrefix + "PwWOs" };

            WOsTable.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            WOsTable.Columns.Add(new DataColumn() { ColumnName = "DateRequested" });
            WOsTable.Columns.Add(new DataColumn() { ColumnName = "PartsOnTraveler" });
        }
        private void CreateWOPartsTable()
        {
            WOParts = new DataTable() { TableName = TablePrefix + "PwWOParts" };

            WOParts.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            WOParts.Columns.Add(new DataColumn() { ColumnName = "PN" });
            WOParts.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            WOParts.Columns.Add(new DataColumn() { ColumnName = "Parent" });
            WOParts.Columns.Add(new DataColumn() { ColumnName = "Qty" });
        }
        private void CreateWOModelsTable()
        {
            WOModels = new DataTable() { TableName = TablePrefix + "PwWOModels" };

            WOModels.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            WOModels.Columns.Add(new DataColumn() { ColumnName = "PN" });
            WOModels.Columns.Add(new DataColumn() { ColumnName = "MaterialPN" });
            WOModels.Columns.Add(new DataColumn() { ColumnName = "PcQty" });
            WOModels.Columns.Add(new DataColumn() { ColumnName = "Qty" });

        }
        private void PopulateWOsSet(IEnumerable<object> objects, int maxQty)
        {
            // Return Table bound to relationships
            var MarkerSet = GetCutTicketsSet(objects);
            var ScheduleSet = GetScheduleSet(objects);
            if (ScheduleSet != null)
            {
                var TravelerTable = GetTravelerTableFromScheduleSet(ScheduleSet);
                var containsXRefs = GetPartsTableFromScheduleSet(ScheduleSet).ColumnNames().Contains("xRefParent");

                foreach (var travelerRow in TravelerTable.AsEnumerable())
                {
                    var earliestRequestDate = travelerRow.GetChildRows("Orders").Select(s => Convert.ToDateTime(s.Field<object>("DateRequested").ToString())).OrderByDescending(o => o).FirstOrDefault();
                    var travelerID = travelerRow.Field<object>("TravelerID").ToString();
                    var travelerQty = travelerRow.GetChildRows("Parts").Sum(s => Convert.ToInt32(s.Field<object>("Qty").ToString()));

                    // Populate the WO Table
                    var newRow = WOsTable.NewRow();
                    newRow.SetField("TravelerID", travelerID);
                    newRow.SetField("DateRequested", earliestRequestDate.ToString("MM/dd/yyyy"));
                    newRow.SetField("PartsOnTraveler", travelerQty);
                    WOsTable.Rows.Add(newRow);


                    var partList = PopulateWOPartsTable(travelerRow, containsXRefs, maxQty);
                    //PopulateWOMarkersTable(MarkerSet, partList);

                }

                if (maxQty > 0)
                {
                    // Renumber travelers by max qty
                    var newTravelersAndParts = RenumberTravelerIDsByMaxQty(maxQty);

                    // Remove the current travelers and add in the new.
                    foreach (var tnp in newTravelersAndParts.Travelers.AsEnumerable())
                    {
                        var tnpTravelerID = tnp.Field<object>("TravelerID").ToString();
                        var tnpTravelerIDToMatch = string.Empty;
                        if (tnpTravelerID.Contains("_"))
                            tnpTravelerIDToMatch = tnpTravelerID.Substring(0, tnpTravelerID.IndexOf('_'));
                        else
                            tnpTravelerIDToMatch = tnpTravelerID;

                        // Remove the traveler
                        var matchedTraveler = WOsTable.AsEnumerable().Where(w => w.Field<object>("TravelerID").ToString() == tnpTravelerIDToMatch);
                        for (int i = 0; i < matchedTraveler.Count(); i++)
                            WOsTable.Rows.Remove(matchedTraveler.ElementAt(i));

                        // Remove the parts
                        var matchedTravelerParts = WOParts.AsEnumerable().Where(w => w.Field<object>("TravelerID").ToString() == tnpTravelerIDToMatch);
                        for (int i = 0; i < matchedTravelerParts.Count(); i++)
                            WOParts.Rows.Remove(matchedTravelerParts.ElementAt(i));

                        // Add back in the new traveler rows
                        var newTravelerRow = WOsTable.NewRow();
                        newTravelerRow.SetField("TravelerID", tnpTravelerID);
                        newTravelerRow.SetField("DateRequested", tnp.Field<object>("DateRequested").ToString());
                        newTravelerRow.SetField("PartsOnTraveler", tnp.Field<object>("PartsOnTraveler").ToString());

                        // Check if it exists already
                        var exists = WOsTable.AsEnumerable()
                            .Where(w => w.Field<object>("TravelerID").ToString() == tnpTravelerID);

                        if (!exists.Any())
                            WOsTable.Rows.Add(newTravelerRow);

                    }

                    // Add back in the new part rows
                    foreach (var tnp in newTravelersAndParts.Parts.AsEnumerable())
                    {
                        var newPartRow = WOParts.NewRow();
                        newPartRow.SetField("TravelerID", tnp.Field<object>("TravelerID").ToString());
                        newPartRow.SetField("PN", tnp.Field<object>("PN").ToString());
                        newPartRow.SetField("Desc1", tnp.Field<object>("Desc1").ToString());
                        newPartRow.SetField("Parent", tnp.Field<object>("Parent").ToString());
                        newPartRow.SetField("Qty", tnp.Field<object>("Qty").ToString());
                        WOParts.Rows.Add(newPartRow);
                    }
                }


            }





        }
        private void PopulateWOMarkersTable(DataSet markerSet, IEnumerable<(string PN, string TravelerID)> partsList)
        {
            var modelsTable = GetModelsTableFromCutTicketSet(markerSet)?.AsEnumerable();
            if (modelsTable != null)
            {
                foreach (var partNum in partsList)
                {
                    var travelerMatches = modelsTable
                        .Where(w =>
                            w.Field<object>("TravelerID").ToString() == partNum.TravelerID
                            && w.Field<object>("Name").ToString() == partNum.PN);

                    if (travelerMatches.Any())
                    {
                        var markers = travelerMatches.Select(s => new
                        {
                            TravelerID = s.Field<object>("TravelerID").ToString(),
                            PN = s.Field<object>("Name").ToString(),
                            MaterialPN = s.Field<object>("Material").ToString(),
                            PcQty = s.Field<object>("PcQty").ToString(),
                            Qty = Convert.ToDecimal(s.Field<object>("Qty").ToString())
                        })
                        .GroupBy(g => new
                        {
                            TravelerID = g.TravelerID,
                            PN = g.PN,
                            PcQty = g.PcQty,
                            MaterialPN = g.MaterialPN
                        })
                        .Select(end => new
                        {
                            TravelerID = end.Key.TravelerID,
                            PN = end.Key.PN,
                            MaterialPN = end.Key.MaterialPN,
                            PcQty = end.Key.PcQty,
                            Qty = end.Sum(add => add.Qty)
                        });

                        foreach (var marker in markers)
                        {
                            var newRow = WOModels.NewRow();
                            newRow.SetField("TravelerID", marker.TravelerID);
                            newRow.SetField("PN", marker.PN);
                            newRow.SetField("MaterialPN", marker.MaterialPN);
                            newRow.SetField("Qty", marker.Qty);
                            newRow.SetField("PcQty", marker.PcQty);
                            WOModels.Rows.Add(newRow);
                        }
                    }
                    else
                    {
                        // Search against the WO parts

                        var newRow = WOModels.NewRow();
                        newRow.SetField("TravelerID", partNum.TravelerID);
                        newRow.SetField("PN", partNum.PN);
                        newRow.SetField("MaterialPN", "CUT TICKET NOT FOUND!");
                        newRow.SetField("PcQty", "0");
                        newRow.SetField("Qty", "0");
                        WOModels.Rows.Add(newRow);
                    }
                }

            }



        }
        public static IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 30)
        {
            for (int i = 0; i < locations.Count; i += nSize)
            {
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
            }
        }
        private IEnumerable<(string PN, string TravelerID)> PopulateWOPartsTable(DataRow travelerRow, bool containsXRefs, int maxQty)
        {
            var ret = new HashSet<(string PN, string TravelerID)>();
            // Get the Parts
            var parts = travelerRow.GetChildRows("Parts")
                .Select(s => new
                {
                    TravelerID = s.Field<object>("TravelerID").ToString(),
                    PN = s.Field<object>("PN")?.ToString(),
                    Desc1 = s.Field<object>("Desc1").ToString(),
                    Parent = containsXRefs ? s.Field<object>("xRefParent")?.ToString() : string.Empty,
                    Qty = Convert.ToInt32(s.Field<object>("Qty")?.ToString())
                })
                .GroupBy(g => new
                {
                    TravelerID = g.TravelerID,
                    PN = g.PN
                })
                .Select(end => new
                {
                    TravelerID = end.Key.TravelerID,
                    PN = end.Key.PN,
                    Desc1 = string.Join(Environment.NewLine, end.Select(sel => sel.Desc1).Distinct()),
                    Parent = string.Join(Environment.NewLine, end.Select(sel => sel.Parent).Distinct()),
                    Qty = end.Sum(add => add.Qty)
                }).Distinct();


            foreach (var part in parts)
            {
                var newRow = WOParts.NewRow();
                newRow.SetField("TravelerID", part.TravelerID);
                newRow.SetField("PN", part.PN);
                newRow.SetField("Desc1", part.Desc1);
                newRow.SetField("Parent", part.Parent);
                newRow.SetField("Qty", part.Qty);
                WOParts.Rows.Add(newRow);

                ret.Add((part.PN, part.TravelerID));
            }
            return ret;
        }


        private bool CutTicketExistInWOs(string TravelerID) => WOParts.AsEnumerable().Where(w => w.Field<object>("TravelerID").ToString() == TravelerID).Any();
        private bool CutTicketPartExistInWOs(string TravelerID, string PN) => WOParts.AsEnumerable().Where(w => w.Field<object>("TravelerID").ToString() == TravelerID).Any();


        #endregion

        #region Bus Kits Set
        private DataTable BusKitsTable { get; set; }
        private DataTable BusKitPartsTable { get; set; }
        private DataTable BusKitPartMatsTable { get; set; }


        private void CreateBusKitsTable()
        {
            BusKitsTable = new DataTable() { TableName = TablePrefix + "PwBusKits" };

            BusKitsTable.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            BusKitsTable.Columns.Add(new DataColumn() { ColumnName = "OrderNumbers" });
            BusKitsTable.Columns.Add(new DataColumn() { ColumnName = "DateRequested" });
            BusKitsTable.Columns.Add(new DataColumn() { ColumnName = "Customer" });
            BusKitsTable.Columns.Add(new DataColumn() { ColumnName = "BusID" });
        }
        private void CreateBusKitPartsTable()
        {
            BusKitPartsTable = new DataTable() { TableName = TablePrefix + "PwBusKitParts" };

            BusKitPartsTable.Columns.Add(new DataColumn() { ColumnName = "BusID" });
            BusKitPartsTable.Columns.Add(new DataColumn() { ColumnName = "OrderNumbers" });
            BusKitPartsTable.Columns.Add(new DataColumn() { ColumnName = "PN" });
            BusKitPartsTable.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            BusKitPartsTable.Columns.Add(new DataColumn() { ColumnName = "Qty" });

        }

        private void CreateBusKitPartMatsTable()
        {
            BusKitPartMatsTable = new DataTable() { TableName = TablePrefix + "PwBusKitPartMats" };

            BusKitPartMatsTable.Columns.Add(new DataColumn() { ColumnName = "PN" });
            BusKitPartMatsTable.Columns.Add(new DataColumn() { ColumnName = "Parent" });
            BusKitPartMatsTable.Columns.Add(new DataColumn() { ColumnName = "OrderNumbers" });
            BusKitPartMatsTable.Columns.Add(new DataColumn() { ColumnName = "BusID" });
            BusKitPartMatsTable.Columns.Add(new DataColumn() { ColumnName = "MaterialPN" });
            BusKitPartMatsTable.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            BusKitPartMatsTable.Columns.Add(new DataColumn() { ColumnName = "Amount" });
        }

        private void PopulateBusKitSet(IEnumerable<object> objects)
        {
            // Return Table bound to relationships
            var MarkerSet = GetCutTicketsSet(objects);
            var ScheduleSet = GetScheduleSet(objects);
            if (ScheduleSet != null)
            {
                var TravelerTable = GetTravelerTableFromScheduleSet(ScheduleSet);
                var OrdersTable = GetOrdersTableFromScheduleSet(ScheduleSet);
                var containsXRefs = GetPartsTableFromScheduleSet(ScheduleSet).ColumnNames().Contains("xRefParent");
                var partsTable = GetPartsTableFromScheduleSet(ScheduleSet);
                var hasXrefs = partsTable.ColumnNames().Contains("xRefParent");

                // Get a unique list of orders
                var combinedOrders = OrdersTable.AsEnumerable()
                    .GroupBy(g => new
                    {
                        Opt1 = g.Field<object>("Opt1")?.ToString(),
                        Opt2 = g.Field<object>("Opt2")?.ToString()
                    });



                foreach (var orderRow in combinedOrders)
                {
                    var allPartsOnOrders = new List<DataRow>();
                    var travelerIDs = string.Join(" | ", orderRow.Select(s => s.Field<object>("TravelerID").ToString()).Distinct().OrderBy(o => o).ToArray());
                    var orderNumbers = string.Join(" | ", orderRow.Select(s => s.Field<object>("OrderNumber").ToString()).Distinct().OrderBy(o => o).ToArray());
                    var DatesRequested = orderRow.Select(s => Convert.ToDateTime(s.Field<object>("DateRequested").ToString())).OrderBy(o => o).FirstOrDefault().ToString("MM/dd/yyyy");
                    var customers = string.Join(" | ", orderRow.Select(s => s.Field<object>("Customer").ToString()).Distinct().OrderBy(o => o).ToArray());

                    var busID = string.Empty;
                    if (!string.IsNullOrEmpty(orderRow.Key.Opt1)) busID = orderRow.Key.Opt1;
                    if (!string.IsNullOrEmpty(orderRow.Key.Opt2)) busID = string.IsNullOrEmpty(busID) ? orderRow.Key.Opt2 : busID = "(" + busID + " | " + orderRow.Key.Opt2 + ")";


                    // Populate the BusKits Table
                    var newRow = BusKitsTable.NewRow();
                    newRow.SetField("TravelerID", travelerIDs);
                    newRow.SetField("DateRequested", DatesRequested);
                    newRow.SetField("OrderNumbers", orderNumbers);
                    newRow.SetField("Customer", customers);
                    newRow.SetField("BusID", busID);
                    BusKitsTable.Rows.Add(newRow);

                    // populate the allPartsOnOrders
                    foreach (var order in orderRow)
                        allPartsOnOrders.AddRange(order.GetChildRows("PartsOnOrder"));

                    // Populate the Parts & Mats Table
                    PopulateBusKitPartsTable(allPartsOnOrders, containsXRefs, busID, orderNumbers, hasXrefs);
                }




            }


        }
        private void PopulateBusKitPartsTable(IEnumerable<DataRow> partRows, bool containsXRefs, string busID, string orderNumbers, bool hasXRef)
        {
            if (partRows.Any())

                if (hasXRef)
                {
                    var parts = partRows.GroupBy(g => new
                    {
                        Parent = hasXRef ? g.Field<object>("xRefParent").ToString() : string.Empty,
                        Desc1 = g.Field<object>("Desc1").ToString()
                    })
                    .Select(g => new
                    {
                        TravelerID = string.Join(" | ", g.Select(s => s.Field<object>("TravelerID").ToString()).Distinct().OrderBy(o => o).ToArray()),
                        PN = g.Key.Parent,
                        Desc = g.Key.Desc1,
                        Qty =  Convert.ToDecimal(g.FirstOrDefault()?.Field<object>("Qty")?.ToString()),
                        Materials = g.Select(sel => sel.GetChildRows("MaterialsForPart"))
                    });


                    foreach (var part in parts)
                    {
                        var newRow = BusKitPartsTable.NewRow();
                        newRow.SetField("OrderNumbers", orderNumbers);
                        newRow.SetField("BusID", busID);
                        newRow.SetField("PN", part.PN);
                        newRow.SetField("Desc1", part.Desc);
                        newRow.SetField("Qty", part.Qty);
                        BusKitPartsTable.Rows.Add(newRow);

                        // Populate the materials for this part
                        var allMaterials = new List<DataRow>();
                        foreach (var partMatList in part.Materials)
                            allMaterials.AddRange(partMatList);

                        PopulateBusKitPartMatsTable(allMaterials, busID, orderNumbers, part.PN);
                    }
                }
                else // If Table doesn't have a cross-reference
                {
                    var parts = partRows.GroupBy(g => new
                    {
                        PN = hasXRef ? g.Field<object>("PN").ToString() : string.Empty,
                        Desc1 = g.Field<object>("Desc1").ToString()
                    })
                    .Select(g => new
                    {
                        TravelerID = string.Join(" | ", g.Select(s => s.Field<object>("TravelerID").ToString()).Distinct().OrderBy(o => o).ToArray()),
                        PN = g.Key.PN,
                        Desc = g.Key.Desc1,
                        Qty = g.Sum(s => Convert.ToDecimal(s.Field<object>("Qty").ToString())),
                        Materials = g.Select(sel => sel.GetChildRows("MaterialsForPart"))
                    });


                    foreach (var part in parts)
                    {
                        var newRow = BusKitPartsTable.NewRow();
                        newRow.SetField("OrderNumbers", orderNumbers);
                        newRow.SetField("BusID", busID);
                        newRow.SetField("PN", part.PN);
                        newRow.SetField("Desc1", part.Desc);
                        newRow.SetField("Qty", part.Qty);
                        BusKitPartsTable.Rows.Add(newRow);

                        // Populate the materials for this part
                        var allMaterials = new List<DataRow>();
                        foreach (var partMatList in part.Materials)
                            allMaterials.AddRange(partMatList);


                        PopulateBusKitPartMatsTable(allMaterials, busID, orderNumbers, part.PN);
                    }
                }


        }
        private void PopulateBusKitPartMatsTable(IEnumerable<DataRow> materialRows, string busID, string orderNumbers, string pn)
        {

            if (!materialRows.Any())
            {
                // No Children found... which means this should be "pulled from stock" as it wasn't cut.
                var newRow = BusKitPartMatsTable.NewRow();
                newRow.SetField("OrderNumbers", orderNumbers);
                newRow.SetField("BusID", busID);
                newRow.SetField("PN", "No Child!");
                newRow.SetField("Parent", pn);
                newRow.SetField("MaterialPN", "");
                newRow.SetField("Desc1", "Pull Parent from Stock.");
                newRow.SetField("Amount", "0");

                BusKitPartMatsTable.Rows.Add(newRow);
                return;
            }

            // Group the material rows by PN
            var matsGrouped = materialRows.GroupBy(g => new
            {
                ChildPN = g.Field<object>("PN").ToString(),
                MaterialPN = g.Field<object>("MaterialPN").ToString(),
                Desc1 = g.Field<object>("Desc1").ToString(),
            })
            .Select(s => new
            {
                MaterialPN = s.Key.MaterialPN,
                ChildPN = s.Key.ChildPN,
                Desc = s.Key.Desc1,
                Amount = s.Sum(sel => Convert.ToDecimal(sel.Field<object>("Amount").ToString()))
            });

            foreach (var material in matsGrouped)
            {
                // Check if there are duplicates, cases where no Opt fields are used, and sum values
                var newRow = BusKitPartMatsTable.NewRow();
                newRow.SetField("OrderNumbers", orderNumbers);
                newRow.SetField("BusID", busID);
                newRow.SetField("PN", material.ChildPN == pn ? "No Child!" : material.ChildPN);
                newRow.SetField("Parent", pn);
                newRow.SetField("MaterialPN", material.ChildPN == pn ? string.Empty : pn);
                newRow.SetField("Desc1", material.ChildPN == pn ? "Pull from Stock." : material.Desc);
                newRow.SetField("Amount", Math.Round(material.Amount, 3));

                BusKitPartMatsTable.Rows.Add(newRow);
            }

            

        }

        #endregion



        #region CoverSummary Table
        private DataTable CutSummary { get; set; }

        private void CreateCutSummaryTable()
        {
            CutSummary = new DataTable() { TableName = TablePrefix + "PwCutSummary" };

            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });

            CutSummary.Columns.Add(new DataColumn() { ColumnName = "Material" });

            CutSummary.Columns.Add(new DataColumn() { ColumnName = "LaborStd" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "VinylStd" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "VinylPlan" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "VinylActual" });

            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TotalPlys" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TotalSpreads" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TotalMarkers" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TotalPartsFromMarkers" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TotalPerim" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TotalParts" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "TotalMaterials" });
            CutSummary.Columns.Add(new DataColumn() { ColumnName = "AvgMarkerEff" });

        }

        private void PopulateCutSummary(IEnumerable<object> objects)
        {

            var ScheduleSet = GetScheduleSet(objects);
            var TravelerTable = GetTravelerTableFromScheduleSet(ScheduleSet);
            var scrubSet = GetScrubSet(objects);

            var partTable = GetPartsTableFromScheduleSet(scrubSet);


            // Get the Traveler, Total Parts, Standard Vinyl from TravelersTable
            foreach (var travelerRow in TravelerTable?.AsEnumerable())
            {
                // Vars to load
                var travelerID = string.Empty;
                int totalParts = 0;
                decimal totalVinylStd = 0;
                decimal totalLaborStd = 0;
                double avgMarkerEff = 0;
                int totalPlys = 0;
                int totalMarkers = 0;
                int totalPartsFromMarker = 0;
                int totalMaterials = 0;
                int totalSpreads = 0;
                decimal plannedVinyl = 0;
                decimal actualVinyl = 0;
                decimal totalPerim = 0;

                var travelerParts = travelerRow.GetChildRows("Parts");

                travelerID = travelerRow.Field<object>("TravelerID").ToString();

                totalParts += travelerParts.Sum(add => Convert.ToInt32(add.Field<object>("Qty").ToString()));

                // Find matching TravelerID on "opsPwVinylOrders" and populate "Material" field.
                var rowMatch = VinylOrders.AsEnumerable()
                    .Where(w => w.Field<object>("TravelerIDs").ToString().Contains(travelerID));

                var travelerMaterial = "n/a";
                var vinylItems = new HashSet<string>();
                foreach (var vMatch in rowMatch)
                {
                    vinylItems.Add(vMatch.Field<object>("Desc1").ToString());
                }
                travelerMaterial = string.Join(" | ", vinylItems);


                foreach (var part in travelerParts)
                {
                    
                    var partParent = string.Empty;
                    var hasXRef = part.Table.ColumnNames().Contains("xRefParent");

                    var partMaterials = part.GetChildRows("MaterialsForPart");

                    totalVinylStd += partMaterials.Sum(add => Convert.ToDecimal(add.Field<object>("Amount").ToString()));
                    // Match the parent row up to be sure numbers are correct!!!!!!


                    var partLabors = part.GetChildRows("LaborsForPart");
                    totalLaborStd += partLabors
                        .Where(w => w.Field<object>("Desc").ToString().ToLower().Contains("cut"))
                        .Sum(add => Convert.ToDecimal(add.Field<object>("RLS").ToString()));
                }
                

                // Get the Avg Plys, total plys total markers, planned vinyl, and actual vinyl
                var MarkerSet = GetCutTicketsSet(objects);
                if (MarkerSet != null)
                {
                    var MarkersTable = GetMarkersTableFromCutTicketSet(MarkerSet).AsEnumerable()
                        .Where(w => w.Field<object>("TravelerID").ToString() == travelerID);

                    totalMarkers += MarkersTable.Count();

                    foreach (var markerRow in MarkersTable)
                    {
                        var markerMaterials = markerRow.GetChildRows("MarkerMaterials");
                        totalMaterials = markerMaterials.Count();
                        plannedVinyl += Convert.ToDecimal(markerRow.Field<object>("TotalPlannedFabricIN").ToString());
                        actualVinyl += Convert.ToDecimal(markerRow.Field<object>("TotalActualFabricIN").ToString());


                        foreach (var markerMatieral in markerMaterials)
                        {
                            totalSpreads += Convert.ToInt32(markerMatieral.Field<object>("SpreadQty").ToString());
                            totalPlys += Convert.ToInt32(markerMatieral.Field<object>("PlyQty").ToString()) * Convert.ToInt32(markerMatieral.Field<object>("SpreadQty").ToString());

                            var markerMaterialModels = markerMatieral.GetChildRows("ModelsFromMaterial");
                            foreach (var matModel in markerMaterialModels)
                            {
                                totalPerim += GetModelsTableFromCutTicketSet(MarkerSet).AsEnumerable()
                                .Where(w => w.Field<object>("TravelerID").ToString() == travelerID
                                    && w.Field<object>("Name").ToString() == matModel.Field<object>("Name").ToString())
                                .Sum(w => (Convert.ToDecimal(w.Field<object>("Perimeter").ToString()) * Convert.ToInt32(matModel.Field<object>("Qty").ToString())) * Convert.ToInt32(markerMatieral.Field<object>("SpreadQty").ToString()));
                            }


                        }


                    }

                    // Get the total marker parts
                    totalPartsFromMarker += GetModelsTableFromCutTicketSet(MarkerSet).AsEnumerable()
                        .Where(w => w.Field<object>("TravelerID").ToString() == travelerID)
                        .Sum(w => Convert.ToInt32(w.Field<object>("Qty").ToString()));

                    // Add the marker average
                    var markerEffList = new List<double>();
                    var markerEffs = GetNestStatusTableFromCutTicketSet(MarkerSet).AsEnumerable()
                        .Where(w => w.Field<object>("TravelerID").ToString() == travelerID);
                    foreach (var row in markerEffs)
                    {
                        var markerEff = Convert.ToDouble(row.Field<object>("MarkerEff").ToString());
                        for (int i = 0; i < totalSpreads; i++)
                            markerEffList.Add(markerEff);
                    }
                    if (markerEffList.Any())
                        avgMarkerEff = markerEffList.Average();
                }

                // Populate Table
                var newRow = CutSummary.NewRow();
                newRow.SetField("TravelerID", travelerID);
                newRow.SetField("Material", travelerMaterial);
                newRow.SetField("LaborStd", Math.Round(totalLaborStd, 3));
                newRow.SetField("VinylStd", Math.Round(totalVinylStd, 3));
                newRow.SetField("VinylPlan", Math.Round(plannedVinyl, 3));
                newRow.SetField("VinylActual", Math.Round(actualVinyl, 3));
                newRow.SetField("TotalPlys", totalPlys);
                newRow.SetField("TotalMarkers", totalMaterials);
                newRow.SetField("TotalPerim", Math.Round(totalPerim, 3));
                newRow.SetField("TotalParts", totalParts);
                newRow.SetField("TotalSpreads", totalSpreads);
                newRow.SetField("TotalMaterials", totalMaterials);
                newRow.SetField("AvgMarkerEff", avgMarkerEff);
                newRow.SetField("TotalPartsFromMarkers", totalPartsFromMarker);

                if (totalPartsFromMarker > 0)
                    CutSummary.Rows.Add(newRow);

            }



        }
        private void RePopulateCutPNandDesc(IEnumerable<object> objects)
        {

            var ScheduleSet = GetScheduleSet(objects);
            var TravelerTable = GetTravelerTableFromScheduleSet(ScheduleSet);
            var scrubSet = GetScrubSet(objects);

            var partTable = GetopsPartsTableFromScheduleSet(ScheduleSet);

            // Get the Avg Plys, total plys total markers, planned vinyl, and actual vinyl
            var MarkerSet = GetCutTicketsSet(objects);
            if (MarkerSet != null)
            {
                var MarkersTable = GetMarkerModelsTableFromCutTicketSet(MarkerSet).AsEnumerable();
                if (MarkersTable != null)
                {
                    foreach (var marker in MarkersTable)
                    {
                        var markerPN = marker.Field<object>("Name").ToString();
                        // Find the matching Traveler
                        var travelerParts = partTable.AsEnumerable()
                            .Where(w => w.Field<object>("TravelerID").ToString() == marker.Field<object>("TravelerID").ToString());

                        var newParents = new HashSet<string>();
                        var newDesc1 = new HashSet<string>();
                        var newDesc2 = new HashSet<string>();
                        // Get the Traveler Parts
                        if (travelerParts != null)
                        {

                            // Find a matching part and get the correct descriptions
                            foreach (DataRow travelerPart in travelerParts)
                            {
                                var pn = travelerPart.Field<object>("PN").ToString();
                                var parentPN = travelerPart.Field<object>("xRefParent")?.ToString();
                                var desc = travelerPart.Field<object>("Desc1").ToString();
                                var desc2 = travelerPart.Field<object>("Desc2").ToString();
                                var pnMinus2 = pn.Substring(0, pn.Length - 2);
                                if (pnMinus2 == markerPN && !string.IsNullOrEmpty(parentPN)) // Only adds if marker PN is the same
                                {
                                    newParents.Add(parentPN);
                                    newDesc1.Add(desc);
                                    newDesc2.Add(desc2);
                                }
                            }

                            

                        }

                        if (newParents.Any()) 
                        {
                            marker.SetField("Parent", string.Join(" | ", newParents));
                            marker.SetField("Desc1", string.Join(" | ", newDesc1));
                            marker.SetField("Desc2", string.Join(" | ", newDesc2));
                        }

                        
                    }
                    
                }

               
            }
        }

        

        #endregion

        #region VinylOrders Table
        private DataTable VinylOrders { get; set; }

        private void CreateVinylOrderTable()
        {
            VinylOrders = new DataTable() { TableName = TablePrefix + "PwVinylOrders" };

            VinylOrders.Columns.Add(new DataColumn() { ColumnName = "TravelerIDs" });
            VinylOrders.Columns.Add(new DataColumn() { ColumnName = "PN" });
            VinylOrders.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            VinylOrders.Columns.Add(new DataColumn() { ColumnName = "Desc2" });

            VinylOrders.Columns.Add(new DataColumn() { ColumnName = "Yardage" });

        }
        private void PopulateVinylOrders(IEnumerable<object> objects)
        {

            var ScheduleSet = GetScheduleSet(objects);
            var TravelerTable = GetTravelerTableFromScheduleSet(ScheduleSet);

            var allMaterials = new List<TravelerMaterial>();
            foreach (var travelerRow in TravelerTable.AsEnumerable())
            {
                var travelerID = travelerRow.Field<object>("TravelerID").ToString();

                var travelerMaterials = travelerRow.GetChildRows("Materials")
                    .GroupBy(g => new
                    {
                        MaterialPN = g.Field<object>("MaterialPN").ToString(),
                        Desc1 = g.Field<object>("Desc1").ToString(),
                        Desc2 = g.Field<object>("Desc2").ToString()
                    })
                    .Select(s => new TravelerMaterial
                    {
                        TravelerID = travelerID,
                        MaterialPN = s.Key.MaterialPN,
                        Desc1 = s.Key.Desc1,
                        Desc2 = s.Key.Desc2,
                        Yardage = Math.Round(s.Sum(add => Convert.ToDecimal(add.Field<object>("Amount").ToString())), 3)
                    });

                allMaterials.AddRange(travelerMaterials);

            }

            // Group all the materials from each ticket by material PN
            var allMatsGroups = allMaterials
                .GroupBy(g => new
                {
                    MaterialPN = g.MaterialPN,
                    Desc1 = g.Desc1,
                    Desc2 = g.Desc2
                })
                .Select(s => new TravelerMaterial
                {
                    TravelerID = string.Join(" | ", s.Select(sel => sel.TravelerID)),
                    MaterialPN = s.Key.MaterialPN,
                    Desc1 = s.Key.Desc1,
                    Desc2 = s.Key.Desc2,
                    Yardage = s.Sum(add => add.Yardage)
                });

            foreach (var material in allMatsGroups)
            {
                var newRow = VinylOrders.NewRow();
                newRow.SetField("TravelerIDs", material.TravelerID);
                newRow.SetField("PN", material.MaterialPN);
                newRow.SetField("Desc1", material.Desc1);
                newRow.SetField("Desc2", material.Desc2);
                newRow.SetField("Yardage", material.Yardage);
                VinylOrders.Rows.Add(newRow);
            }



        }

        private class TravelerMaterial
        {
            public string TravelerID { get; set; }
            public string MaterialPN { get; set; }
            public string Desc1 { get; set; }
            public string Desc2 { get; set; }
            public decimal Yardage { get; set; }
        }
        #endregion

        #region TravelerSummary Table

        private DataTable TravelerSummary { get; set; }
        private DataTable LaborsSummary { get; set; }
        private DataTable LaborColumns { get; set; }
        private DataTable LaborTotals { get; set; }

        private void CreateTravelerSummaryTable()
        {
            TravelerSummary = new DataTable() { TableName = TablePrefix + "PwTravelerSummary" };

            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });

            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "Parent" });
            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "Desc2" });

            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "VinylStd" });
            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "VinylPlan" });
            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "VinylActual" });

            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "TotalPartsFromMarkers" });
            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "TotalParts" });
            

            TravelerSummary.Columns.Add(new DataColumn() { ColumnName = "Labors", DataType = typeof(object[]) });

        }
        private void CreateLaborsSummaryTable()
        {
            LaborsSummary = new DataTable() { TableName = TablePrefix + "PwLaborsSummary" };

            //! Column names are added later

        }
        private void CreateLaborColumnNamesTable()
        {
            LaborColumns = new DataTable() { TableName = TablePrefix + "PwLaborRoutesForSummary" };

            LaborColumns.Columns.Add(new DataColumn() { ColumnName = "Route" });

        }
        private void CreateLaborTotalsTable()
        {
            LaborTotals = new DataTable() { TableName = TablePrefix + "PwLaborTotalsForSummary" };

            LaborTotals.Columns.Add(new DataColumn() { ColumnName = "Total" });

        }
        private void PopulateTravelerSummary(IEnumerable<object> objects)
        {

            var ScheduleSet = GetScheduleSet(objects);
            var TravelerTable = GetTravelerTableFromScheduleSet(ScheduleSet);
            var scrubSet = GetScrubSet(objects);

            var partTable = GetPartsTableFromScheduleSet(scrubSet);
            var laborTable = GetLaborsTableFromScheduleSet(scrubSet);



            // Get unique list of labors to inject and display as column headers
            var uniqueLaborHeaders = laborTable.AsEnumerable()
                .Select(w => w.Field<object>("Desc").ToString()).Distinct();

            // Add each labor string to a unique LaborSummary table
            foreach (var laborDesc in uniqueLaborHeaders)
            {
                LaborsSummary.Columns.Add(new DataColumn() { ColumnName = laborDesc, DefaultValue = 0 });
                var laborCol = LaborColumns.NewRow();
                laborCol.SetField("Route", laborDesc);
                LaborColumns.Rows.Add(laborCol);
            }


            // Get the Traveler, Total Parts, Standard Vinyl from TravelersTable
            foreach (var travelerRow in TravelerTable?.AsEnumerable())
            {
                // Vars to load
                var travelerID = string.Empty;
                int totalParts = 0;
                int totalSpreads = 0;
                int totalPartsFromMarker = 0;
                decimal totalVinylStd = 0;
                decimal actualVinyl = 0;
                var labors = new List<string>();
                double avgMarkerEff = 0;



                var travelerParts = travelerRow.GetChildRows("Parts");

                travelerID = travelerRow.Field<object>("TravelerID").ToString();

                totalParts += travelerParts.Sum(add => Convert.ToInt32(add.Field<object>("Qty").ToString()));

                var newLaborRow = LaborsSummary.NewRow();
                foreach (var part in travelerParts)
                {

                    var partParent = string.Empty;
                    var hasXRef = part.Table.ColumnNames().Contains("xRefParent");

                    var partMaterials = part.GetChildRows("MaterialsForPart");

                    totalVinylStd += partMaterials.Sum(add => Convert.ToDecimal(add.Field<object>("Amount").ToString()));
                    // Match the parent row up to be sure numbers are correct!!!!!!

                    // Add each labor to respective column
                    var partLabors = part.GetChildRows("LaborsForPart");
                    
                    foreach (var labor in partLabors)
                    {
                        var desc = labor.Field<object>("Desc")?.ToString();
                        var rls = Convert.ToDecimal(labor.Field<object>("RLS").ToString());
                        
                        newLaborRow.SetField(desc, Convert.ToDecimal(newLaborRow.Field<object>(desc).ToString()) +  rls);
                    }
                    
                }
                LaborsSummary.Rows.Add(newLaborRow);


                // Get the Avg Plys, total plys total markers, planned vinyl, and actual vinyl
                var MarkerSet = GetCutTicketsSet(objects);
                if (MarkerSet != null)
                {
                    var MarkersTable = GetMarkersTableFromCutTicketSet(MarkerSet).AsEnumerable()
                        .Where(w => w.Field<object>("TravelerID").ToString() == travelerID);

                    foreach (var markerRow in MarkersTable)
                    {
                        var markerMaterials = markerRow.GetChildRows("MarkerMaterials");
                        actualVinyl += Convert.ToDecimal(markerRow.Field<object>("TotalActualFabricIN").ToString());

                    }

                    // Get the total marker parts
                    totalPartsFromMarker += GetModelsTableFromCutTicketSet(MarkerSet).AsEnumerable()
                        .Where(w => w.Field<object>("TravelerID").ToString() == travelerID)
                        .Sum(w => Convert.ToInt32(w.Field<object>("Qty").ToString()));
                }


                // Find matching TravelerID on "opsPwVinylOrders" and populate "Material" field.
                var rowMatch = VinylOrders.AsEnumerable()
                    .Where(w => w.Field<object>("TravelerIDs").ToString().Contains(travelerID));

                var travelerMaterial = "n/a";
                var vinylItems = new HashSet<string>();
                foreach (var vMatch in rowMatch)
                {
                    vinylItems.Add(vMatch.Field<object>("Desc1").ToString());
                }
                travelerMaterial = string.Join(" | ", vinylItems);

                // Find matching TravelerID on "opsPwVinylOrders" and populate "Material" field.
                var parentMatch = WOParts.AsEnumerable()
                    .Where(w => w.Field<object>("TravelerID").ToString().Contains(travelerID)
                    || w.Field<object>("TravelerID").ToString().Equals(travelerID));

                var parent = "n/a";
                var parentDesc = "n/a";
                var parentItems = new HashSet<string>();
                var parentDescItems = new HashSet<string>();
                foreach (var vMatch in parentMatch)
                {
                    parentItems.Add(vMatch.Field<object>("Parent").ToString());
                    parentDescItems.Add(vMatch.Field<object>("Desc1").ToString());
                }
                parent = string.Join(" | ", parentItems);
                parentDesc = string.Join(" | ", parentDescItems);


                // Populate Table
                var newRow = TravelerSummary.NewRow();
                newRow.SetField("TravelerID", travelerID);
                newRow.SetField("Parent", parent);
                newRow.SetField("Desc1", parentDesc);
                newRow.SetField("Desc2", travelerMaterial);
                newRow.SetField("VinylStd", Math.Round(totalVinylStd, 3));
                newRow.SetField("VinylActual", Math.Round(actualVinyl, 3));
                newRow.SetField("TotalParts", totalParts);
                newRow.SetField("TotalPartsFromMarkers", totalPartsFromMarker);
                newRow.SetField("Labors", newLaborRow.ItemArray);
                TravelerSummary.Rows.Add(newRow);

            }

            // Get the totals
            foreach (var laborDesc in LaborColumns.AsEnumerable())
            {
                var routeName = laborDesc.Field<object>("Route").ToString();
                var laborMatches = LaborsSummary.AsEnumerable().Select(w => Convert.ToDecimal(w.Field<object>(routeName).ToString()));
                var laborTotals = LaborTotals.NewRow();
                laborTotals.SetField("Total", Math.Round(laborMatches.Sum(s => s), 3));
                LaborTotals.Rows.Add(laborTotals);
            }
            



        }

        #endregion

        private string GetPartParent(DataRow dr)
        {
            if (dr == null) return null;
            if (dr.Table.ColumnNames().Contains("xRefParent"))
                return dr.Field<object>("xRefParent").ToString();

            return string.Empty;

        }
        private DataTable GetOrdersTableFromScheduleSet(DataSet ds)
        {
            if (ds == null) return null;
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("Orders"))
                {
                    return table;
                }


            return null;
        }
        private DataTable GetModelsTableFromCutTicketSet(DataSet ds)
        {
            if (ds == null) return null;
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("Models"))
                {
                    return table;
                }


            return null;
        }
        private DataTable GetMarkersTableFromCutTicketSet(DataSet ds)
        {
            if (ds == null) return null;
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("Markers"))
                {
                    return table;
                }


            return null;
        }
        private DataTable GetMarkerModelsTableFromCutTicketSet(DataSet ds)
        {
            if (ds == null) return null;
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("MarkerModels"))
                {
                    return table;
                }


            return null;
        }
        private DataTable GetNestStatusTableFromCutTicketSet(DataSet ds)
        {
            if (ds == null) return null;
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("NestingStatus"))
                {
                    return table;
                }


            return null;
        }

        private DataTable GetTravelerTableFromScheduleSet(DataSet ds)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            if (ds == null) return null;
            foreach (DataTable table in ds?.Tables)
                if (table.TableName.Contains("Travelers"))
                {
                    return table;
                }


            return null;
        }

        private HashSet<string> GetPartsList(DataRow rowInfo, DataTable partTable)
        {
            // Get tjhe part row from the partTable
            var searchPN = rowInfo.Table.ColumnNames("xRef", false).Any()
                ? rowInfo.Field<object>("xRefParent").ToString()
                : rowInfo.Field<object>("PN").ToString();

            var partRow = partTable.AsEnumerable().Where(w => w.Field<object>("PN").ToString() == searchPN).FirstOrDefault();


            // Check if there are Cross-References and add the column if so
            var xRefVals = new HashSet<string>();
            var xRefCols = partRow.Table.ColumnNames("XRef", false);
            if (xRefCols.Any())
            {
                foreach (var colName in xRefCols)
                {
                    var colVal = partRow.Field<object>(colName)?.ToString();
                    if (!string.IsNullOrEmpty(colVal))
                        xRefVals.Add(colVal);
                }
            }

            if (!xRefVals.Any())
                xRefVals.Add(searchPN); // If No Xref then add the normal PN

            return xRefVals;
        }


        private DataTable GetopsPartsTableFromScheduleSet(DataSet ds)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName != "Parts" && table.TableName.Contains("Parts"))
                {
                    return table;
                }


            return null;
        }
        private DataTable GetPartsTableFromScheduleSet(DataSet ds)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("Parts"))
                {
                    return table;
                }


            return null;
        }
        private DataTable GetPartsOnOrderTableFromScheduleSet(DataSet ds)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("PartsOnOrder"))
                {
                    return table;
                }


            return null;
        }
        private DataTable GetLaborsTableFromScheduleSet(DataSet ds)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (DataTable table in ds.Tables)
                if (table.TableName.Contains("Labors"))
                {
                    return table;
                }


            return null;
        }
        private DataSet GetScheduleSet(IEnumerable<object> objects)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (var obj in objects)
                if (obj.GetObjectName().Contains("Schedule"))
                {
                    return obj as DataSet;
                }


            return null;
        }
        private DataSet GetScrubSet(IEnumerable<object> objects)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (var obj in objects)
                if (obj.GetObjectName().Contains("ScrubSet"))
                {
                    return obj as DataSet;
                }


            return null;
        }
        private DataSet GetCutTicketsSet(IEnumerable<object> objects)
        {
            // Return the "Travelers" table via BindingSource so relationships are filled.
            foreach (var obj in objects)
                if (obj.GetObjectName().Contains("CutTickets"))
                {
                    return obj as DataSet;
                }


            return null;
        }


        #endregion




    }
}
