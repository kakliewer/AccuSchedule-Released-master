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
using DataTableExtensions = AccuSchedule.UI.Extensions.DataTableExtensions;
using AccuSchedule.UI.Methods;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class BuildSchedulesTool : ToolPlugin
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

        #region Injections
        private enum BasedOnEnum
        {
            Orders = 1,
            Materials = 2,
            Parts = 3
        }

        private static string[] BasedOnList
        {
            get
            {
                // Conver the enum to a list of strings
                var ret = Enum.GetValues(typeof(BasedOnEnum)).Cast<object>().Select(s => s.ToString()).ToArray();

                AddObjectToInject(ret); // Set the objects to inject
                return ret;
            }
        }
        #endregion


        // Main Function
        public DataSet BuildSchedule(DataSet _set, Action<DataSet> Build, string ScheduleName, string _Combo_BasedOn_BasedOnList)
        {
            if (_set == null) return null;

            if (string.IsNullOrEmpty(_Combo_BasedOn_BasedOnList) || string.IsNullOrEmpty(ScheduleName)) return null;

            var baseTable = DetermineBaseTable(_set, _Combo_BasedOn_BasedOnList);
            if (baseTable == null) return null;

            var baseGroupings = GetBaseGroupings(baseTable, _Combo_BasedOn_BasedOnList);

            var groupedWithID = GetNewTableWithIDs(baseGroupings, ScheduleName, _Combo_BasedOn_BasedOnList);

            var scheduleSet = CreateNewDataSet(ScheduleName);

            PopulateData(scheduleSet, groupedWithID, _Combo_BasedOn_BasedOnList, ScheduleName, _set);
           
            return scheduleSet;
        }


        #region Main Tables
        private DataTable TicketsTable { get; set; }
        private DataTable OrdersTable { get; set; }
        private DataTable PartsTable { get; set; }
        private DataTable MaterialsTable { get; set; }
        private DataTable LaborsTable { get; set; }

        private DataTable NoXRefsTable { get; set; }
        #endregion

        #region Data Population
        private void PopulateTravelerID(DataSet schedule, DataTable baseGrouping)
        {
            var TravelerIDs = baseGrouping.AsEnumerable().Select(s => s.Field<object>("TravelerID").ToString());

            foreach (var travelerID in TravelerIDs)
                TicketsTable.Rows.Add(travelerID);
        } 
        private void PopulateData(DataSet schedule, DataTable baseGrouping, string BasedOnMethod, string ScheduleName, DataSet originalSet)
        {

            _ = Enum.TryParse(BasedOnMethod, out BasedOnEnum enumValue);


            // Determine target table schedule will be based on
            switch (enumValue)
            {
                case BasedOnEnum.Orders:
                    PopulateTravelerID(schedule, baseGrouping);
                    PopulateBasedOnOrders(schedule, baseGrouping, originalSet, ScheduleName);
                    PopulateTablesBasedOffGroupedOrders(schedule, originalSet, true, true, true);
                    break;
                case BasedOnEnum.Materials:
                    PopulateTravelerID(schedule, baseGrouping);
                    PopulateBasedOnMaterials(schedule, baseGrouping, originalSet, ScheduleName);
                    PopulateTablesBasedOffGroupedOrders(schedule, originalSet, true, true, true);
                    break;
                case BasedOnEnum.Parts:
                    PopulateTravelerID(schedule, baseGrouping);
                    PopulateBasedOnParts(schedule, baseGrouping, originalSet, ScheduleName);
                    PopulateTablesBasedOffGroupedOrders(schedule, originalSet, true, true, false);
                    break;
                default:
                    break;
            }

            var hasXRefs = PartsTable.ColumnNames().Contains("xRefParent");
            AddRelationships(schedule, hasXRefs);

        }


        /// <summary>
        ///  Must have Orders and Parts tables populated prior to executing.
        /// </summary>
        /// <param name="schedule"></param>
        /// <param name="originalSet"></param>
        /// <param name="addMaterials"></param>
        /// <param name="addLabors"></param>
        private void PopulateTablesBasedOffGroupedOrders(DataSet schedule, DataSet originalSet, bool addMaterials, bool addLabors, bool addParts)
        {
            var origOrdersTable = originalSet.Tables["Orders"];
            var origPartsTable = originalSet.Tables["Parts"];
            var origPartsOnOrderTable = originalSet.Tables["PartsOnOrder"];
            var origMaterialsTable = originalSet.Tables["Materials"];
            var origMaterialsOnOrderTable = originalSet.Tables["MaterialsOnOrder"];
            var origLaborsTable = originalSet.Tables["Labors"];
            var origLaborsOnOrderTable = originalSet.Tables["LaborsOnOrder"];

            var hasXrefCol = origPartsTable.ColumnNames("XRef", false).Any();
            // Add the xref cols to the other tables if existing
            if (hasXrefCol && !MaterialsTable.ColumnNames("xRefParent", false, true).Any())
            {
                MaterialsTable.Columns.Add(new DataColumn() { ColumnName = "xRefParent" });
                LaborsTable.Columns.Add(new DataColumn() { ColumnName = "xRefParent" });
            }

            DataTable allParts = null;
            DataTable allNoXrefs = null;
            foreach (var order in OrdersTable.AsEnumerable())
            {
                var TravelerID = order.Field<object>("TravelerID").ToString();
                var OrderNumber = order.Field<object>("OrderNumber").ToString();

                DataTable prePartsTable = null;
                if (addParts)
                {
                    var res = GetPartRows(origPartsOnOrderTable, origPartsTable, OrderNumber, TravelerID);
                    prePartsTable = res.Parts;
                    // Add the parts
                    if (allParts == null)
                        allParts = prePartsTable;
                    else
                        allParts.Merge(prePartsTable);
                    // Add the noXrefs
                    if (allNoXrefs == null)
                        allNoXrefs = res.NoXrefs;
                    else
                        allNoXrefs.Merge(res.NoXrefs);
                } else
                {
                    prePartsTable = PartsTable;
                }

                

            }

            // When complete, if parts were added, then group to avoid duplicates when all complete
            if (addParts)
            {
                GroupAndPopulatePartsTableWithIDs(allParts, origPartsTable);
                GroupAndPopulateNoXrefTable(allNoXrefs);
            }

            if (addMaterials || addLabors)
            {
                foreach (var order in OrdersTable.AsEnumerable())
                {
                    var TravelerID = order.Field<object>("TravelerID").ToString();
                    var OrderNumber = order.Field<object>("OrderNumber").ToString();

                    // Add Material and Labor
                    var parts = PartsTable?.AsEnumerable()
                    .Where(w => w.Field<object>("TravelerID").ToString() == TravelerID
                    && w.Field<object>("OrderNumber").ToString() == OrderNumber);

                    if (parts != null)
                    {
                        foreach (var part in parts)
                        {
                            var PN = !hasXrefCol ? part.Field<object>("PN").ToString() : string.IsNullOrEmpty(part.Field<object>("xRefParent")?.ToString()) ? part.Field<object>("PN").ToString() : part.Field<object>("xRefParent")?.ToString();
                            var parent = hasXrefCol ? string.IsNullOrEmpty(part.Field<object>("xRefParent")?.ToString()) ? part.Field<object>("PN").ToString() : part.Field<object>("xRefParent")?.ToString() : string.Empty;
                            var child = part.Field<object>("PN").ToString();

                            if (addMaterials)
                            {
                                AddMaterialRow(originalSet, TravelerID, OrderNumber, PN, parent, child);
                            }

                            if (addLabors)
                            {
                                AddLaborRow(originalSet, TravelerID, OrderNumber, PN, parent, child);
                            }

                        }
                    }
                }
            }


            }

        private HashSet<string> GetPartsList(DataRow rowInfo, string PN, DataTable mainTable)
        {
            // Check if there are Cross-References and add the column if so
            var xRefVals = new HashSet<string>();
            var xRefCols = rowInfo?.Table.ColumnNames("XRef", false);

            if (xRefCols != null && xRefCols.Any())
            {
                foreach (var colName in xRefCols)
                {
                    var colVal = rowInfo.Field<object>(colName)?.ToString();
                    if (!string.IsNullOrEmpty(colVal))
                        xRefVals.Add(colVal);
                }

                if (!mainTable.ColumnNames().Contains("xRefParent"))
                    mainTable.Columns.Add(new DataColumn() { ColumnName = "xRefParent" });
            }

            if (!xRefVals.Any())
                xRefVals.Add(string.Empty); // If No Xref then add the normal PN

            return xRefVals;
        }

        private DataRow AddOrderRow(DataSet originalSet, string TravelerID, string OrderNumber)
        {
            DataRow ret = null;
            var origOrdersTable = originalSet.Tables["Orders"];

            if (!OrdersTable.AsEnumerable().Any(a =>
                a.Field<object>("TravelerID").ToString() == TravelerID
                && a.Field<object>("OrderNumber").ToString() == OrderNumber))
            {
                // Populate Order Table
                var matchedOrderInfo = origOrdersTable.AsEnumerable().Where(w => w.Field<object>("OrderNumber").ToString().Trim() == OrderNumber.Trim()).FirstOrDefault();
                var newOrderRow = OrdersTable.NewRow();
                newOrderRow.SetField("TravelerID", TravelerID);
                newOrderRow.SetField("OrderNumber", OrderNumber);
                newOrderRow.SetField("Customer", matchedOrderInfo?.Field<object>("Customer").ToString());
                newOrderRow.SetField("DateRequested", matchedOrderInfo?.Field<object>("DateRequested").ToString());
                newOrderRow.SetField("Opt1", matchedOrderInfo?.Field<object>("Opt1")?.ToString());
                newOrderRow.SetField("Opt2", matchedOrderInfo?.Field<object>("Opt2")?.ToString());

                OrdersTable.Rows.Add(newOrderRow);
                ret = newOrderRow;
            }
            return ret;
        }
        private void AddPartRow(DataRow part, DataSet originalSet, string TravelerID)
        {
            var origPartsTable = originalSet.Tables["Parts"];
            var origPartsOnOrderTable = originalSet.Tables["PartsOnOrder"];

            var OrderNumber = part.Field<object>("OrderNumber").ToString();
            var PN = part.Field<object>("PN").ToString();


            if (!PartsTable.AsEnumerable()
                            .Any(a =>
                                a.Field<object>("OrderNumber").ToString() == OrderNumber
                                && a.Field<object>("TravelerID").ToString() == TravelerID
                                && a.Field<object>("PN").ToString() == PN
                                || a.Field<object>("OrderNumber").ToString() == OrderNumber
                                && a.Field<object>("TravelerID").ToString() == TravelerID
                                && PartsTable.ColumnNames().Contains("xRefParent") ? a.Field<object>("xRefParent")?.ToString() == PN : false))
            {
                // Populate Parts Table
                var PartInfo = origPartsTable.AsEnumerable().Where(w => w.Field<object>("PN").ToString() == PN).FirstOrDefault();

                // Check if there are Cross-References and add the column if so
                var xRefVals = GetPartsList(PartInfo, PN, PartsTable);

                foreach (var pnVal in xRefVals)
                {
                    var newPartRow = PartsTable.NewRow();
                    newPartRow.SetField("TravelerID", TravelerID);
                    newPartRow.SetField("OrderNumber", OrderNumber);
                    newPartRow.SetField("PN", string.IsNullOrEmpty(pnVal) ? PN : pnVal);
                    newPartRow.SetField("Qty", part.Field<object>("Qty").ToString());
                    newPartRow.SetField("Desc1", PartInfo.Field<object>("Desc1").ToString());
                    newPartRow.SetField("Desc2", PartInfo.Field<object>("Desc2").ToString());

                    if (PartsTable.ColumnNames().Contains("xRefParent"))
                        newPartRow.SetField("xRefParent", PN);

                     PartsTable.Rows.Add(newPartRow);

                    if (string.IsNullOrEmpty(pnVal))
                    {
                        // Add to the "No XRef" table
                        var newNoXrefPartRow = NoXRefsTable.NewRow();
                        newNoXrefPartRow.SetField("TravelerID", TravelerID);
                        newNoXrefPartRow.SetField("OrderNumber", OrderNumber);
                        newNoXrefPartRow.SetField("PN", PN);
                        newNoXrefPartRow.SetField("Qty", part.Field<object>("Qty").ToString());
                        newNoXrefPartRow.SetField("Desc1", PartInfo.Field<object>("Desc1").ToString());
                        newNoXrefPartRow.SetField("Desc2", PartInfo.Field<object>("Desc2").ToString());

                        NoXRefsTable.Rows.Add(newNoXrefPartRow);
                    }


                }

                
            }
        }
        private void AddMaterialRow(DataSet originalSet, string TravelerID, string orderNumber, string PN, string Parent, string Child)
        {
            var origPartsTable = originalSet.Tables["Parts"];
            var origMaterialsTable = originalSet.Tables["Materials"];
            var origMaterialsOnOrderTable = originalSet.Tables["MaterialsOnOrder"];

            // Populate Materials Table
            var pnSearch = string.IsNullOrEmpty(Parent) ? PN : Parent;
            var matchedMaterialsForPart = origMaterialsOnOrderTable.AsEnumerable()
                .Where(w =>
                    w.Field<object>("OrderNumber").ToString() == orderNumber
                    && w.Field<object>("PN").ToString() == pnSearch
                    || w.Field<object>("OrderNumber").ToString() == orderNumber
                    && w.Field<object>("PN").ToString() == pnSearch
                )
                .GroupBy(g => new { MaterialPN = g.Field<object>("MaterialPN"), AccumarkTabCode = g.Field<object>("AccumarkTabCode") });
            foreach (var matForPart in matchedMaterialsForPart)
            {
                var matchedMaterialInfo = origMaterialsTable.AsEnumerable().Where(w =>
                    w.Field<object>("MaterialPN").ToString() == matForPart.Key.MaterialPN.ToString()
                ).FirstOrDefault();

                if (matchedMaterialInfo != null)
                {
                    // Populate Parts Table
                    var PartInfo = origPartsTable.AsEnumerable().Where(w => w.Field<object>("PN").ToString() == pnSearch).FirstOrDefault();
                    // Check if there are Cross-References and add the column if so
                    var xRefVals = GetPartsList(PartInfo, pnSearch, MaterialsTable);

                    if (!string.IsNullOrEmpty(PN))
                    {
                        var pnToAdd = string.IsNullOrEmpty(Child) ? PN : Child;
                        var newMaterialRow = MaterialsTable.NewRow();
                        newMaterialRow.SetField("TravelerID", TravelerID);
                        newMaterialRow.SetField("OrderNumber", orderNumber);
                        newMaterialRow.SetField("PN", pnToAdd);
                        newMaterialRow.SetField("MaterialPN", matForPart.Key.MaterialPN.ToString());
                        newMaterialRow.SetField("Amount", 
                            Math.Round(matForPart.Sum(s =>
                            {
                                var res = Convert.ToDecimal(s.Field<object>("Amount").ToString()) / xRefVals.Count();
                                return res;
                            })
                            , 3));
                        newMaterialRow.SetField("Desc1", matchedMaterialInfo.Field<object>("Desc1")?.ToString());
                        newMaterialRow.SetField("Desc2", matchedMaterialInfo.Field<object>("Desc2")?.ToString());
                        newMaterialRow.SetField("AccumarkFabCode", matchedMaterialInfo.Field<object>("AccumarkFabCode")?.ToString());
                        newMaterialRow.SetField("AccumarkTabCode", matForPart.Key.AccumarkTabCode.ToString());
                        if (MaterialsTable.ColumnNames().Contains("xRefParent"))
                            newMaterialRow.SetField("xRefParent", Parent);

                        // Make sure row isn't added twice.
                        if (!MaterialsTable.AsEnumerable().Any(a =>
                            a.Field<object>("TravelerID")?.ToString() == TravelerID
                            && a.Field<object>("OrderNumber")?.ToString() == orderNumber
                            && a.Field<object>("PN")?.ToString() == pnToAdd
                            && a.Field<object>("MaterialPN")?.ToString() == matForPart.Key.MaterialPN.ToString()
                            && MaterialsTable.ColumnNames().Any(item => item == "xRefParent") ? a.Field<object>("xRefParent").ToString() == PN : false
                            ))
                            MaterialsTable.Rows.Add(newMaterialRow);
                    }
                    
                }
            }
        }
        private void AddLaborRow(DataSet originalSet, string TravelerID, string orderNumber, string PN, string Parent, string Child)
        {
            var origPartsTable = originalSet.Tables["Parts"];
            var origLaborsTable = originalSet.Tables["Labors"];
            var origLaborsOnOrderTable = originalSet.Tables["LaborsOnOrder"];

            // Populate Labors Table
            var pnSearch = string.IsNullOrEmpty(Parent) ? PN : Parent;
            var matchedLaborsForPart = origLaborsOnOrderTable.AsEnumerable()
                .Where(w =>
                    w.Field<object>("OrderNumber").ToString() == orderNumber
                    && w.Field<object>("PN").ToString() == pnSearch
                )
                .GroupBy(g => new { Desc = g.Field<object>("Desc"), Seq = g.Field<object>("SequenceNo"), OrderNumber = g.Field<object>("OrderNumber").ToString(), PN = g.Field<object>("PN").ToString() });

            // Populate Parts Table
            var PartInfo = origPartsTable.AsEnumerable().Where(w => w.Field<object>("PN").ToString() == pnSearch).FirstOrDefault();

            // Check if there are Cross-References and add the column if so
            var xRefVals = GetPartsList(PartInfo, pnSearch, LaborsTable);
            

            foreach (var laborForPart in matchedLaborsForPart)
            {

                if (!string.IsNullOrEmpty(PN))
                {
                    var pnToAdd = string.IsNullOrEmpty(Child) ? PN : Child;
                    var newLaborRow = LaborsTable.NewRow();
                    newLaborRow.SetField("TravelerID", TravelerID);
                    newLaborRow.SetField("OrderNumber", orderNumber);
                    newLaborRow.SetField("PN", pnToAdd);
                    newLaborRow.SetField("SequenceNo", laborForPart.Key.Seq.ToString());
                    newLaborRow.SetField("Desc", laborForPart.Key.Desc.ToString());
                    newLaborRow.SetField("RMS", Math.Round(laborForPart.Sum(s => Convert.ToDecimal(s.Field<object>("RMSTotal").ToString())) / xRefVals.Count, 3));
                    newLaborRow.SetField("RLS", Math.Round(laborForPart.Sum(s => Convert.ToDecimal(s.Field<object>("RLSTotal").ToString())) / xRefVals.Count, 3));
                    newLaborRow.SetField("SLHS", Math.Round(laborForPart.Sum(s => Convert.ToDecimal(s.Field<object>("SLHSTotal").ToString())) / xRefVals.Count, 3));
                    if (LaborsTable.ColumnNames().Contains("xRefParent"))
                        newLaborRow.SetField("xRefParent", Parent);

                    // Make sure row isn't added twice.
                    
                    if (!LaborsTable.AsEnumerable().Any(a =>
                        a.Field<object>("TravelerID").ToString() == TravelerID
                        && a.Field<object>("OrderNumber").ToString() == orderNumber
                        && a.Field<object>("PN").ToString() == pnToAdd
                        && a.Field<object>("SequenceNo").ToString() == laborForPart.Key.Seq.ToString()
                        && a.Field<object>("Desc").ToString() == laborForPart.Key.Desc.ToString()
                        ))
                        LaborsTable.Rows.Add(newLaborRow);
                }
                
            }
            
        }
        #endregion

        #region BasedOn Groupings
        private void PopulateBasedOnOrders(DataSet schedule, DataTable baseGrouping, DataSet originalSet, string ScheduleName)
        { // TravelerID, PN, OrderNumber, Qty
            var origOrdersTable = originalSet.Tables["Orders"];
            var origPartsTable = originalSet.Tables["Parts"];
            var origPartsOnOrderTable = originalSet.Tables["PartsOnOrder"];
            var origMaterialsTable = originalSet.Tables["Materials"];
            var origMaterialsOnOrderTable = originalSet.Tables["MaterialsOnOrder"];
            var origLaborsTable = originalSet.Tables["Labors"];
            var origLaborsOnOrderTable = originalSet.Tables["LaborsOnOrder"];

            // Get the Part Information
            var errList = new List<DataRow>();
            var ordersGrouped = baseGrouping.AsEnumerable();


            foreach (var groupedRow in ordersGrouped)
            {
                // Get a list of all the order numbers grouped
                var orderNumbersRaw = groupedRow.Field<object>("OrderNumber").ToString();

                var orderNumbers = new List<string>();
                if (orderNumbersRaw.Contains("|"))
                    orderNumbers.AddRange(orderNumbersRaw.Split('|'));
                else
                    orderNumbers.Add(orderNumbersRaw);

                var TravelerID = groupedRow.Field<object>("TravelerID").ToString();


                // Add the Orders and parts
                var allParts = PartsTable.Clone();
                var allNoXrefs = NoXRefsTable.Clone();
                foreach (var orderNumber in orderNumbers.Distinct())
                {
                    AddOrderRow(originalSet, TravelerID, orderNumber);
                    //AddPartRows(allParts, origPartsOnOrderTable, origPartsTable, orderNumber, TravelerID);
                }

            }


            




        }
        private (DataTable Parts, DataTable NoXrefs) GetPartRows(DataTable origPartsOnOrderTable, DataTable origPartsTable, string orderNumber, string TravelerID)
        {
            // Loop through all the parts with matching order numbers
            var parts = origPartsOnOrderTable.AsEnumerable()
                .Where(w => w.Field<object>("OrderNumber").ToString() == orderNumber);

            var hasXrefs = origPartsTable.ColumnNames("XRef", false).Any();

            var allParts = PartsTable.Clone();
            var allNoXrefs = NoXRefsTable.Clone();

            foreach (var partRow in parts)
            {
                var PN = partRow.Field<object>("PN").ToString();
                var Qty = partRow.Field<object>("Qty").ToString();

                // Populate Parts Table
                var matchedPartInfo = origPartsTable.AsEnumerable().Where(w => w.Field<object>("PN").ToString() == PN).FirstOrDefault();

                // Check if there are Cross-References and add the column if so
                var xRefVals = GetPartsList(matchedPartInfo, PN, allParts);

                foreach (var pnVal in xRefVals)
                {
                    //ToolsExtensions.GenerateID()
                    var newPartRow = allParts.NewRow();
                    newPartRow.SetField("TravelerID", TravelerID);
                    newPartRow.SetField("OrderNumber", orderNumber);
                    newPartRow.SetField("PN", string.IsNullOrEmpty(pnVal) ? PN : pnVal);
                    newPartRow.SetField("Qty", partRow.Field<object>("Qty").ToString());
                    newPartRow.SetField("Desc1", matchedPartInfo.Field<object>("Desc1").ToString());
                    newPartRow.SetField("Desc2", matchedPartInfo.Field<object>("Desc2").ToString());
                    
                    if (hasXrefs)
                        newPartRow.SetField("xRefParent", PN);

                    allParts.Rows.Add(newPartRow);

                    // Set no XREF row if not found
                    if (string.IsNullOrEmpty(pnVal))
                    {
                        // Add to the "No XRef" table
                        var newNoXrefPartRow = allNoXrefs.NewRow();
                        newNoXrefPartRow.SetField("TravelerID", TravelerID);
                        newNoXrefPartRow.SetField("OrderNumber", orderNumber);
                        newNoXrefPartRow.SetField("PN", PN);
                        newNoXrefPartRow.SetField("Qty", partRow.Field<object>("Qty").ToString());
                        newNoXrefPartRow.SetField("Desc1", matchedPartInfo.Field<object>("Desc1").ToString());
                        newNoXrefPartRow.SetField("Desc2", matchedPartInfo.Field<object>("Desc2").ToString());
                        allNoXrefs.Rows.Add(newNoXrefPartRow);
                    }

                }


            }

            return (allParts, allNoXrefs);
            // Group and Populate the partsTable
            
        }
        private void PopulateBasedOnMaterials(DataSet schedule, DataTable baseGrouping, DataSet originalSet, string ScheduleName)
        { 
            var origOrdersTable = originalSet.Tables["Orders"];
            var origPartsTable = originalSet.Tables["Parts"];
            var origPartsOnOrderTable = originalSet.Tables["PartsOnOrder"];
            var origMaterialsTable = originalSet.Tables["Materials"];
            var origMaterialsOnOrderTable = originalSet.Tables["MaterialsOnOrder"];
            var origLaborsTable = originalSet.Tables["Labors"];
            var origLaborsOnOrderTable = originalSet.Tables["LaborsOnOrder"];

            // Get the Part Information
            var errList = new List<DataRow>();
            var partsGrouped = baseGrouping.AsEnumerable();

            // Unique Material PN's
            foreach (var groupedRow in partsGrouped)
            {
                // Get a list of all the order numbers grouped
                var orderNumbersRaw = groupedRow.Field<object>("OrderNumber").ToString();
                var materialPN = groupedRow.Field<object>("MaterialPN").ToString();

                var orderNumbers = new List<string>();
                if (orderNumbersRaw.Contains("|"))
                    orderNumbers.AddRange(orderNumbersRaw.Split('|'));
                else
                    orderNumbers.Add(orderNumbersRaw);
                
                var TravelerID = groupedRow.Field<object>("TravelerID").ToString();

                // Add the Orders
                foreach (var orderNumber in orderNumbers.Distinct())
                {
                    AddOrderRow(originalSet, TravelerID, orderNumber);

                   
                }


            }

        }
        private void PopulateBasedOnParts(DataSet schedule, DataTable baseGrouping, DataSet originalSet, string ScheduleName)
        {
            var origOrdersTable = originalSet.Tables["Orders"];
            var origPartsTable = originalSet.Tables["Parts"];
            var origPartsOnOrderTable = originalSet.Tables["PartsOnOrder"];
            var origMaterialsTable = originalSet.Tables["Materials"];
            var origMaterialsOnOrderTable = originalSet.Tables["MaterialsOnOrder"];
            var origLaborsTable = originalSet.Tables["Labors"];
            var origLaborsOnOrderTable = originalSet.Tables["LaborsOnOrder"];

            // Get the Part Information
            var errList = new List<DataRow>();
            var partsGrouped = baseGrouping.AsEnumerable();

            
            foreach (var groupedRow in partsGrouped)
            {
                // Get a list of all the order numbers grouped
                var orderNumbersRaw = groupedRow.Field<object>("OrderNumber").ToString();
                var PN = groupedRow.Field<object>("PN").ToString();
                var Qty = groupedRow.Field<object>("Qty").ToString();
                var TravelerID = groupedRow.Field<object>("TravelerID").ToString();

                var orderNumbers = new List<string>();
                if (orderNumbersRaw.Contains("|"))
                    orderNumbers.AddRange(orderNumbersRaw.Split('|'));
                else
                    orderNumbers.Add(orderNumbersRaw);

                // Populate Parts Table
                var matchedPartInfo = origPartsTable.AsEnumerable().Where(w => w.Field<object>("PN").ToString() == groupedRow.Field<object>("PN").ToString()).FirstOrDefault();

                // Check if there are Cross-References and add the column if so
                var xRefVals = GetPartsList(matchedPartInfo, PN, PartsTable);

                // Add the Orders
                foreach (var orderNumber in orderNumbers.Distinct())
                {
                    // Find the parts on the order
                    var parts = origPartsOnOrderTable.AsEnumerable()
                        .Where(w => w.Field<object>("OrderNumber").ToString() == orderNumber
                        && w.Field<object>("PN").ToString() == PN);


                    var allParts = PartsTable.Clone();
                    
                    foreach (var part in parts)
                    {
                        var ogPN = xRefVals.First();
                        var lastPart = string.Empty;
                        foreach (var pnVal in xRefVals)
                        {
                            //ToolsExtensions.GenerateID()
                            var newPartRow = allParts.NewRow();
                            // Assign TravelerID after all orders have been entered -->newPartRow.SetField("TravelerID", TravelerID);
                            newPartRow.SetField("OrderNumber", orderNumber);
                            newPartRow.SetField("PN", pnVal);
                            newPartRow.SetField("Qty", part.Field<object>("Qty").ToString());
                            newPartRow.SetField("Desc1", matchedPartInfo.Field<object>("Desc1").ToString());
                            newPartRow.SetField("Desc2", matchedPartInfo.Field<object>("Desc2").ToString());

                            if (allParts.ColumnNames().Contains("xRefParent"))
                                newPartRow.SetField("xRefParent", PN);

                            allParts.Rows.Add(newPartRow);

                            
                        }
                    }

                    // Group and Populate the partsTable
                    GroupAndPopulatePartsTable(allParts);
                }


                

            }

            //Group the parts table one more time due to cross-referencing.
            AssignIDsToPartsTable(originalSet);
        }

        private void AssignIDsToPartsTable(DataSet originalSet)
        {
            // Get a unique list of all PN's
            var pnList = PartsTable.AsEnumerable().Select(s => s.Field<object>("PN").ToString()).Distinct();

            OrdersTable.Clear();

            var hasXrefs = PartsTable.ColumnNames("xRefParent").Any();

            var noChildCnt = 1;

            TicketsTable.Rows.Clear();

            // Loop through and assign each a unique travelerID
            var allNoXrefs = NoXRefsTable.Clone();
            foreach (var uniquePN in pnList)
            {
                var travelerID = ToolsExtensions.GenerateID();
                
                // Add traveler row
                var newTravelerRow = TicketsTable.NewRow();
                newTravelerRow.SetField("TravelerID", travelerID);
                TicketsTable.Rows.Add(newTravelerRow);


                

                var matchRow = PartsTable.AsEnumerable().Where(w => w.Field<object>("PN").ToString() == uniquePN);
                foreach (var row in matchRow)
                {
                    // Set the traveler Row for all part nbumbers 
                    row.SetField("TravelerID", travelerID);

                    // Add order Row if Travler and Ordernumber don't already exist
                    var orderNum = row.Field<object>("OrderNumber")?.ToString();
                    var matchedOrderNum = OrdersTable.AsEnumerable().Any(w => w.Field<object>("TravelerID").ToString() == travelerID && w.Field<object>("OrderNumber").ToString() == orderNum);
                    if (!matchedOrderNum)
                    {
                        AddOrderRow(originalSet, travelerID, orderNum);
                    }
                    

                    // Set no XREF row if not found
                    if (string.IsNullOrEmpty(row.Field<object>("PN")?.ToString()))
                    {

                        var parent = row.Field<object>("xRefParent")?.ToString();
                        // Set the part row PN to a No Child
                        row.SetField("PN", parent);

                        // Add to the "No XRef" table
                        var newNoXrefPartRow = allNoXrefs.NewRow();
                        newNoXrefPartRow.SetField("TravelerID", travelerID);
                        newNoXrefPartRow.SetField("OrderNumber", orderNum);
                        newNoXrefPartRow.SetField("PN", parent);
                        newNoXrefPartRow.SetField("Qty", row.Field<object>("Qty")?.ToString());
                        newNoXrefPartRow.SetField("Desc1", row.Field<object>("Desc1").ToString());
                        newNoXrefPartRow.SetField("Desc2", row.Field<object>("Desc2").ToString());
                        allNoXrefs.Rows.Add(newNoXrefPartRow);
                        noChildCnt += 1;
                    }
                }
            }

            GroupAndPopulateNoXrefTable(allNoXrefs);

        }
        private void GroupAndPopulatePartsTableWithIDs(DataTable partsTable, DataTable origPartsTable)
        {
            if (partsTable == null) return;

            var hasXrefs = origPartsTable.ColumnNames("XRef", false).Any();

            if (hasXrefs && !PartsTable.ColumnNames().Contains("xRefParent"))
            {
                PartsTable.Columns.Add(new DataColumn() { ColumnName = "xRefParent" });
            }

            // Group the parts one more time
            var allPartsGrouped = partsTable.AsEnumerable()
                .GroupBy(g => new
                {
                    TravelerID = g.Field<object>("TravelerID").ToString(),
                    PN = g.Field<object>("PN").ToString(),
                    Parent = hasXrefs ? g.Field<object>("xRefParent")?.ToString() : g.Field<object>("PN").ToString(),
                    OrderNumber = g.Field<object>("OrderNumber").ToString()
                })
                .Select(s => new
                {
                    TravelerID = s.Key.TravelerID,
                    OrderNumber = s.Key.OrderNumber,
                    PN = s.Key.PN,
                    Desc1 = string.Join("|", s.Select(sel => sel.Field<object>("Desc1").ToString()).Distinct()),
                    Desc2 = string.Join("|", s.Select(sel => sel.Field<object>("Desc2").ToString()).Distinct()),
                    Qty = s.Sum(add => Convert.ToInt32(add.Field<object>("Qty").ToString())),
                    Parent = s.Key.Parent
                });


            // Add back into the main table
            foreach (var part in allPartsGrouped)
            {
                var newPartRow = PartsTable.NewRow();
                newPartRow.SetField("TravelerID", part.TravelerID);
                newPartRow.SetField("OrderNumber", part.OrderNumber);
                newPartRow.SetField("PN", part.PN);
                newPartRow.SetField("Qty", part.Qty);
                newPartRow.SetField("Desc1", part.Desc1);
                newPartRow.SetField("Desc2", part.Desc2);
                if (PartsTable.ColumnNames().Contains("xRefParent") && !string.IsNullOrEmpty(part.Parent))
                    newPartRow.SetField("xRefParent", part.Parent);

                PartsTable.Rows.Add(newPartRow);
            }
        }
        private void GroupAndPopulatePartsTable(DataTable partsTable)
        {
            var hasXrefs = partsTable.ColumnNames().Contains("xRefParent");

            // Group the parts one more time
            var allPartsGrouped = partsTable.AsEnumerable()
                .GroupBy(g => new
                {
                    OrderNumber = g.Field<object>("OrderNumber").ToString(),
                    PN = g.Field<object>("PN").ToString(),
                    Parent = hasXrefs ? g.Field<object>("xRefParent")?.ToString() : g.Field<object>("PN").ToString()
                })
                .Select(s => new
                {
                    OrderNumber = s.Key.OrderNumber,
                    PN = s.Key.PN,
                    Desc1 = string.Join("|", s.Select(sel => sel.Field<object>("Desc1").ToString()).Distinct()),
                    Desc2 = string.Join("|", s.Select(sel => sel.Field<object>("Desc2").ToString()).Distinct()),
                    Qty = s.Sum(add => Convert.ToInt32(add.Field<object>("Qty").ToString())),
                    Parent = s.Key.Parent
                });


            // Add back into the main table
            foreach (var part in allPartsGrouped)
            {
                var newPartRow = PartsTable.NewRow();
                newPartRow.SetField("OrderNumber", part.OrderNumber);
                newPartRow.SetField("PN", part.PN);
                newPartRow.SetField("Qty", part.Qty);
                newPartRow.SetField("Desc1", part.Desc1);
                newPartRow.SetField("Desc2", part.Desc2);
                if (!string.IsNullOrEmpty(part.Parent))
                    newPartRow.SetField("xRefParent", part.Parent);

                PartsTable.Rows.Add(newPartRow);
            }
        }
        private void GroupAndPopulateNoXrefTable(DataTable partsTable)
        {
            var hasXrefs = partsTable.ColumnNames().Contains("xRefParent");

            // Group the parts one more time
            var allPartsGrouped = partsTable.AsEnumerable()
                .GroupBy(g => new
                {
                    PN = g.Field<object>("PN").ToString(),
                    Desc1 = g.Field<object>("Desc1").ToString(),
                    Desc2 = g.Field<object>("Desc2").ToString()
                })
                .Select(s => new
                {
                    TravelerID = string.Join(" | ", s.Select(sel => sel.Field<object>("TravelerID").ToString()).Distinct()),
                    OrderNumber = string.Join(" | ", s.Select(sel => sel.Field<object>("OrderNumber").ToString()).Distinct()),
                    PN = s.Key.PN,
                    Desc1 = s.Key.Desc1,
                    Desc2 = s.Key.Desc2,
                    Qty = s.Sum(add => Convert.ToInt32(add.Field<object>("Qty").ToString()))
                });


            // Add back into the main table
            foreach (var part in allPartsGrouped)
            {
                var newPartRow = NoXRefsTable.NewRow();
                newPartRow.SetField("TravelerID", part.TravelerID);
                newPartRow.SetField("OrderNumber", part.OrderNumber);
                newPartRow.SetField("PN", part.PN);
                newPartRow.SetField("Qty", part.Qty);
                newPartRow.SetField("Desc1", part.Desc1);
                newPartRow.SetField("Desc2", part.Desc2);

                NoXRefsTable.Rows.Add(newPartRow);
            }
        }
        #endregion

        #region Tables and DataSet Creation
        private DataSet CreateNewDataSet(string ScheduleName)
        {
            var ret = new DataSet();

            // Create the tables
            TicketsTable = CreateTravelersTable(ScheduleName);
            OrdersTable = CreateOrdersTable(ScheduleName);
            PartsTable = CreatePartsTable(ScheduleName);
            MaterialsTable = CreateMaterialsTable(ScheduleName);
            LaborsTable = CreateLaborsTable(ScheduleName);

            NoXRefsTable = CreateNoXrefPartsTable(ScheduleName);

            // Add the tables
            ret.Tables.Add(TicketsTable);
            ret.Tables.Add(OrdersTable);
            ret.Tables.Add(PartsTable);
            ret.Tables.Add(MaterialsTable);
            ret.Tables.Add(LaborsTable);
            ret.Tables.Add(NoXRefsTable);

            ret.DataSetName = ScheduleName + "Schedule";

            return ret;
        }
        private void AddRelationships(DataSet schedule, bool hasXRefs)
        {
            // Tickets Table Relationships
            schedule.Relations.Add("Orders", TicketsTable.Columns["TravelerID"], OrdersTable.Columns["TravelerID"]);
            schedule.Relations.Add("Parts", TicketsTable.Columns["TravelerID"], PartsTable.Columns["TravelerID"]);
            schedule.Relations.Add("Materials", TicketsTable.Columns["TravelerID"], MaterialsTable.Columns["TravelerID"]);
            schedule.Relations.Add("Labors", TicketsTable.Columns["TravelerID"], LaborsTable.Columns["TravelerID"]);

            // Orders Table Relationships
            schedule.Relations.Add("PartsOnOrder",
                new DataColumn[] { OrdersTable.Columns["TravelerID"], OrdersTable.Columns["OrderNumber"] },
                new DataColumn[] { PartsTable.Columns["TravelerID"], PartsTable.Columns["OrderNumber"] });
            schedule.Relations.Add("MaterialsOnOrder",
                new DataColumn[] { OrdersTable.Columns["TravelerID"], OrdersTable.Columns["OrderNumber"] },
                new DataColumn[] { MaterialsTable.Columns["TravelerID"], MaterialsTable.Columns["OrderNumber"] });
            schedule.Relations.Add("LaborsOnOrder",
                new DataColumn[] { OrdersTable.Columns["TravelerID"], OrdersTable.Columns["OrderNumber"] },
                new DataColumn[] { LaborsTable.Columns["TravelerID"], LaborsTable.Columns["OrderNumber"] });

            // Parts Table Relationships
            if (!hasXRefs)
            {
                schedule.Relations.Add("MaterialsForPart",
                    new DataColumn[] { PartsTable.Columns["TravelerID"], PartsTable.Columns["OrderNumber"], PartsTable.Columns["PN"] },
                    new DataColumn[] { MaterialsTable.Columns["TravelerID"], MaterialsTable.Columns["OrderNumber"], MaterialsTable.Columns["PN"] });
                schedule.Relations.Add("LaborsForPart",
                    new DataColumn[] { PartsTable.Columns["TravelerID"], PartsTable.Columns["OrderNumber"], PartsTable.Columns["PN"] },
                    new DataColumn[] { LaborsTable.Columns["TravelerID"], LaborsTable.Columns["OrderNumber"], LaborsTable.Columns["PN"] });
            } else
            {
                schedule.Relations.Add("MaterialsForPart",
                    new DataColumn[] { PartsTable.Columns["TravelerID"], PartsTable.Columns["OrderNumber"], PartsTable.Columns["PN"], PartsTable.Columns["xRefParent"] },
                    new DataColumn[] { MaterialsTable.Columns["TravelerID"], MaterialsTable.Columns["OrderNumber"], MaterialsTable.Columns["PN"], MaterialsTable.Columns["xRefParent"] });
                schedule.Relations.Add("LaborsForPart",
                    new DataColumn[] { PartsTable.Columns["TravelerID"], PartsTable.Columns["OrderNumber"], PartsTable.Columns["PN"], PartsTable.Columns["xRefParent"] },
                    new DataColumn[] { LaborsTable.Columns["TravelerID"], LaborsTable.Columns["OrderNumber"], LaborsTable.Columns["PN"], LaborsTable.Columns["xRefParent"] });
            }
        }

        private DataTable CreateTravelersTable(string ScheduleName)
        {
            var ret = new DataTable();
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.TableName = string.Format("{0}{1}", ScheduleName, "Travelers");
            return ret;
        }
        private DataTable CreateOrdersTable(string ScheduleName)
        {
            var ret = new DataTable();
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "OrderNumber" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Customer" });
            ret.Columns.Add(new DataColumn() { ColumnName = "DateRequested" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Opt1" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Opt2" });
            ret.TableName = string.Format("{0}{1}", ScheduleName, "Orders");
            return ret;
        }
        private DataTable CreatePartsTable(string ScheduleName)
        {
            var ret = new DataTable();
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "OrderNumber" });
            ret.Columns.Add(new DataColumn() { ColumnName = "PN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Qty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc2" });
            ret.TableName = string.Format("{0}{1}", ScheduleName, "Parts");
            return ret;
        }
        private DataTable CreateNoXrefPartsTable(string ScheduleName)
        {
            var ret = new DataTable();
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "OrderNumber" });
            ret.Columns.Add(new DataColumn() { ColumnName = "PN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Qty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc2" });
            ret.TableName = string.Format("{0}{1}", ScheduleName, "NoXRefs");
            return ret;
        }
        private DataTable CreateMaterialsTable(string ScheduleName)
        {
            var ret = new DataTable();
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "OrderNumber" });
            ret.Columns.Add(new DataColumn() { ColumnName = "PN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "MaterialPN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Amount" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc2" }); 
            ret.Columns.Add(new DataColumn() { ColumnName = "AccumarkFabCode" });
            ret.Columns.Add(new DataColumn() { ColumnName = "AccumarkTabCode" });

            ret.TableName = string.Format("{0}{1}", ScheduleName, "Materials");
            return ret;
        }
        private DataTable CreateLaborsTable(string ScheduleName)
        {
            var ret = new DataTable();
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "OrderNumber" });
            ret.Columns.Add(new DataColumn() { ColumnName = "PN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "SequenceNo" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc" });
            ret.Columns.Add(new DataColumn() { ColumnName = "RMS" });
            ret.Columns.Add(new DataColumn() { ColumnName = "RLS" });
            ret.Columns.Add(new DataColumn() { ColumnName = "SLHS" });
            ret.TableName = string.Format("{0}{1}", ScheduleName, "Labors");
            return ret;
        }
        #endregion


        #region Helpers
        private DataTable DetermineBaseTable(DataSet _set, string BasedOnMethod)
        {
            _ = Enum.TryParse(BasedOnMethod, out BasedOnEnum enumValue);

            // Determine target table schedule will be based on
            switch (enumValue)
            {
                case BasedOnEnum.Orders:
                    return _set.Tables.Contains("Orders") ? _set.Tables["Orders"] : null;
                case BasedOnEnum.Materials:
                    return _set.Tables.Contains("MaterialsOnOrder") ? _set.Tables["MaterialsOnOrder"] : null;
                case BasedOnEnum.Parts:
                    return _set.Tables.Contains("PartsOnOrder") ? _set.Tables["PartsOnOrder"] : null;
                default:
                    return null;
            }
        }
        private List<IGrouping<dynamic, DataRow>> GetBaseGroupings(DataTable baseTable, string BasedOnMethod)
        {
            _ = Enum.TryParse(BasedOnMethod, out BasedOnEnum enumValue);

            // Determine target table schedule will be based on
            var rows = new List<IGrouping<dynamic, DataRow>>();
            var AllGroupIDCols = new List<string>();

            switch (enumValue)
            {
                case BasedOnEnum.Orders:
                    // Get all GroupID columns
                    AllGroupIDCols = DataTableExtensions.ColumnNames(baseTable, "GroupID", false, false).ToList();
                    if (!AllGroupIDCols.Any()) AllGroupIDCols.Add("OrderNumber");
                    break;
                case BasedOnEnum.Materials:
                    AllGroupIDCols = DataTableExtensions.ColumnNames(baseTable, "GroupID", false, false).ToList();
                    if (!AllGroupIDCols.Any()) AllGroupIDCols.Add("MaterialPN");
                    break;
                case BasedOnEnum.Parts:
                    AllGroupIDCols = DataTableExtensions.ColumnNames(baseTable, "GroupID", false, false).ToList();
                    if (!AllGroupIDCols.Any()) AllGroupIDCols.Add("PN");
                    break;
                default:
                    break;
            }

            var query = baseTable.AsEnumerable()
                        .GroupBy(g => new DynamicDataRowGroup(g, AllGroupIDCols.ToArray()));

            if (query.Any()) rows.AddRange(query.ToList());
            

            return rows;
        }
        private DataTable GetNewTableWithIDs(List<IGrouping<dynamic, DataRow>> groupingQuery, string ScheduleName, string BasedOnMethod)
        {
            DataTable ret = new DataTable();
            var colsToInclude = new Dictionary<string, object>();
            var colsToSum = new Dictionary<string, object>();

            _ = Enum.TryParse(BasedOnMethod, out BasedOnEnum enumValue);

            // Determine target table schedule will be based on
            switch (enumValue)
            {
                case BasedOnEnum.Orders:
                    colsToInclude["OrderNumber"] = 0;
                    colsToInclude["DateRequested"] = 1;
                    colsToInclude["Customer"] = 2;
                    colsToInclude["Opt1"] = 3;
                    colsToInclude["Opt2"] = 4;
                    ret = AddTicketIDs(groupingQuery, ScheduleName, colsToInclude, null);
                    break;
                case BasedOnEnum.Materials:
                    colsToInclude["MaterialPN"] = 0;
                    colsToInclude["PN"] = 1;
                    colsToInclude["OrderNumber"] = 3;
                    colsToSum["Amount"] = 2;
                    ret = AddTicketIDs(groupingQuery, ScheduleName, colsToInclude, colsToSum);
                    break;
                case BasedOnEnum.Parts:
                    colsToInclude["PN"] = 0;
                    colsToInclude["OrderNumber"] = 2;
                    colsToSum["Qty"] = 1;
                    ret = AddTicketIDs(groupingQuery, ScheduleName, colsToInclude, colsToSum);
                    break;
                default:
                    break;
            }

            return ret;
        }

        private DataTable AddTicketIDs(List<IGrouping<dynamic, DataRow>> groupingQuery, string ScheduleName, Dictionary<string, object> ColNamesToInclude, Dictionary<string, object> ColNamesToSum)
        {
            // Create the ID
            var tableQuery = groupingQuery.Select(s => new
            {
                ID = ToolsExtensions.GenerateID(),
                Rows = s.ToList().Select(r => r)
            });

            // Add columns
            var newTable = DataTableExtensions.NewTableWithOrganizedColumns(ColNamesToInclude, ColNamesToSum);
            newTable.TableName = ScheduleName;
            var TravelerCol = new DataColumn() { ColumnName = "TravelerID" };
            newTable.Columns.Add(TravelerCol);
            TravelerCol.SetOrdinal(0);


            // Populate the rows
            foreach (var traveler in tableQuery)
            {
                var newRow = newTable.NewRow();
                newRow.SetField("TravelerID", traveler.ID);

                if (ColNamesToInclude != null)
                    foreach (var colName in ColNamesToInclude)
                        newRow.SetField(colName.Key, string.Join("|", traveler.Rows.Select(s => s.Field<object>(colName.Key)?.ToString()).Distinct()));
                if (ColNamesToSum != null)
                    foreach (var colName in ColNamesToSum)
                        newRow.SetField(colName.Key, Math.Round(traveler.Rows.Sum(s => Convert.ToDecimal(s.Field<object>(colName.Key))), 3));

                newTable.Rows.Add(newRow);
            }

            return newTable;
        }
        #endregion 


    }
}
