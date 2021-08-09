using AccuSchedule.UI.Extensions;
using AccuSchedule.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AccuSchedule.Plugins.Tools
{
    public class ScrubWOsTool : ToolPlugin
    {
        #region BoilerPlate
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


        public HashSet<object> FunctionObjectsToLoad => null;
        #endregion



        public DataSet ScrubWOs(DataTable _table)
        {
            // Get the main data into a workable format
            GetAllOrders(_table);
            GetAllParts(_table);
            GetAllCustomers(_table);
            GetAllLabors(_table);
            GetAllMaterials(_table);

            // Create the dataset
            CreateDataSet();

            // Process Order Info
            GetOrderPartInfo(_table);
            GetOrderMaterialInfo(_table);
            GetOrderLaborInfo(_table);
            FinalizeOnOrderTables();

            return OrderSet;
        }

        private DataSet OrderSet { get; set; }
        private void CreateDataSet()
        {
            OrderSet = new DataSet();
            OrderSet.DataSetName = "ScrubSet";

            // Add Main Tables
            OrderSet.Tables.Add(Orders);
            OrderSet.Tables.Add(Customers);
            OrderSet.Tables.Add(Parts);
            OrderSet.Tables.Add(Labors);
            OrderSet.Tables.Add(Materials);
        }

        #region Tables
        private DataTable Orders { get; set; }
        private DataTable Customers { get; set; }
        private DataTable Parts { get; set; }
        private DataTable Labors { get; set; }
        private DataTable Materials { get; set; }

        // Order Information
        private DataTable PartsOnOrders { get; set; }
        private DataTable MaterialsOnOrders { get; set; }
        private DataTable LaborsOnOrders { get; set; }
        #endregion


        #region Populate Tables
        private void GetAllOrders(DataTable table)
        {
            // Only get items that have populated rows
            var query = table.AsEnumerable()
                .Where(w => !string.IsNullOrEmpty(w.Field<object>("PlannedStart")?.ToString())
                    && !string.IsNullOrEmpty(w.Field<object>("WONum")?.ToString()))
                .Select(x => new
                {
                    // WO Dates
                    PlannedStart = x.Table.Columns.Contains("PlannedStart") && !string.IsNullOrEmpty(x.Field<object>("PlannedStart").ToString())
                        ? Convert.ToDateTime(x.Field<object>("PlannedStart").ToString()).ToShortDateString()
                        : DateTime.Now.ToShortDateString().ToString(),
                    PlannedCompletion = x.Table.Columns.Contains("PlannedCompletion") && !string.IsNullOrEmpty( x.Field<object>("PlannedCompletion").ToString())
                        ? Convert.ToDateTime(x.Field<object>("PlannedCompletion").ToString()).ToShortDateString()
                        : DateTime.Now.ToShortDateString().ToString(),
                    LastUpdated = x.Table.Columns.Contains("LastUpdated") && !string.IsNullOrEmpty(x.Field<object>("LastUpdated").ToString())
                        ? Convert.ToDateTime(x.Field<object>("LastUpdated").ToString()).ToShortDateString()
                        : DateTime.Now.ToShortDateString().ToString(),
                    WOStatusCode = x.Table.Columns.Contains("WOStatusCode")
                        ? x.Field<object>("WOStatusCode").ToString()
                        : "0",
                    LastStatusChange = x.Table.Columns.Contains("LastStatusChange") && !string.IsNullOrEmpty(x.Field<object>("LastStatusChange").ToString())
                        ? Convert.ToDateTime(x.Field<object>("LastStatusChange").ToString()).ToShortDateString()
                        : DateTime.Now.ToShortDateString().ToString(),

                    // Inheritable fields (don't change the names... need to implement Interface)
                    Customer = x.Table.Columns.Contains("Customer")
                        ? x.Field<object>("Customer").ToString()
                        : string.Empty,
                    OrderNumber = x.Table.Columns.Contains("WONum")
                        ? Convert.ToString(x.Field<object>("WONum").ToString())
                        : "0",
                    DateRequested = x.Table.Columns.Contains("RequestedDate") && !string.IsNullOrEmpty(x.Field<object>("RequestedDate").ToString())
                        ? Convert.ToDateTime(x.Field<object>("RequestedDate").ToString()).ToShortDateString()
                        : x.Table.Columns.Contains("DateRequested") && !string.IsNullOrEmpty(x.Field<object>("DateRequested").ToString())
                            ? Convert.ToDateTime(x.Field<object>("DateRequested").ToString()).ToShortDateString()
                            : DateTime.Now.ToShortDateString().ToString(),

                    Opt1 = x.Table.Columns.Contains("Opt1")
                        ? x.Field<object>("Opt1").ToString()
                        : string.Empty,
                    Opt2 = x.Table.Columns.Contains("Opt2")
                        ? x.Field<object>("Opt2").ToString()
                        : string.Empty,

                    RelatedOrderType = x.Table.Columns.Contains("RelatedOrderType")
                        ? x.Field<object>("RelatedOrderType").ToString()
                        : "0",
                    RelatedOrderNum = x.Table.Columns.Contains("RelatedPoSoNumber")
                        ? x.Field<object>("RelatedPoSoNumber").ToString()
                        : "0",


                }).Distinct().ToList();

            Orders = query.ToTable();
            Orders.TableName = "Orders";
        }
        private void GetAllParts(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("PN") ? x.Field<object>("PN").ToString() : string.Empty,
                    Desc1 = x.Table.Columns.Contains("PNDesc") ? x.Field<object>("PNDesc").ToString() : string.Empty,
                    Desc2 = x.Table.Columns.Contains("PNDesc2") ? x.Field<object>("PNDesc2").ToString() : string.Empty
                }).Distinct().OrderBy(o => o.PN).ToList();

            Parts = query.ToTable();

            Parts.TableName = "Parts";
        }
        private void GetAllCustomers(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    Customer = x.Table.Columns.Contains("Customer")
                        ? x.Field<object>("Customer").ToString()
                        : string.Empty,
                    CustomerNumber = x.Table.Columns.Contains("AddressNumber")
                    ? x.Field<object>("AddressNumber").ToString()
                    : "0"
                }).Distinct().OrderBy(o => o.Customer).ToList();

            Customers = query.ToTable();

            Customers.TableName = "Customers";
        }
        private void GetAllLabors(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    Parent = x.Table.Columns.Contains("PN") ? x.Field<object>("PN").ToString() : string.Empty,
                    SequenceNo = x.Table.Columns.Contains("LaborSeq") ? x.Field<object>("LaborSeq").ToString() : "0",
                    Desc = x.Table.Columns.Contains("LaborDesc") ? x.Field<object>("LaborDesc").ToString() : string.Empty,
                    RLSPer = x.Table.Columns.Contains("LaborPer") ? x.Field<object>("LaborPer").ToString() : "0"
                }).Distinct().OrderBy(o => o.Parent).ToList();

            Labors = query.ToTable();
            Labors.TableName = "Labors";
        }
        private void GetAllMaterials(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    Parent = x.Table.Columns.Contains("PN") ? x.Field<object>("PN").ToString() : string.Empty,
                    BOMSearch = x.Table.Columns.Contains("Material") ? x.Field<object>("Material").ToString() : string.Empty
                }).Distinct().OrderBy(o => o.Parent);

            // Create new based on pn and desc
            var retDT = CreateNewMaterialTable();
            var matList = new List<DataRow>();
            foreach (var item in query)
            {
                var newRow = ProcessBOMSearch(retDT, item.Parent, item.BOMSearch);
                if (newRow != null) matList.Add(newRow);
            }
            matList = matList.Distinct().ToList();

            // Convert back to datatable.
            foreach (var item in matList)
                retDT.Rows.Add(item);
            retDT.TableName = "Materials";

            Materials = retDT;
        }
        #endregion

        #region Populate Order Info
        private void GetOrderPartInfo(DataTable table)
        {
            // Create the table schema
            PartsOnOrders = new DataTable();
            PartsOnOrders.Columns.Add(new DataColumn() { Caption = "Order", DataType = typeof(string) });
            PartsOnOrders.Columns.Add(new DataColumn() { Caption = "Route", DataType = typeof(string) });
            PartsOnOrders.Columns.Add(new DataColumn() { Caption = "PN", DataType = typeof(string) });
            PartsOnOrders.Columns.Add(new DataColumn() { Caption = "Qty", DataType = typeof(int) });
            PartsOnOrders.Columns.Add(new DataColumn() { Caption = "LineType", DataType = typeof(string) });

            // Loop through the new unique table of orders
            foreach (var order in Orders.AsEnumerable())
            {
                var partsOnOrder = GetPartsFromOrder(order, table, order.Field<string>("OrderNumber"));
                PartsOnOrders.Merge(partsOnOrder, false, MissingSchemaAction.Add);
            }
            // Remove the first 4 columns that get automatically added
            try
            {
                PartsOnOrders.Columns.Remove("Column1");
                PartsOnOrders.Columns.Remove("Column2");
                PartsOnOrders.Columns.Remove("Column3");
                PartsOnOrders.Columns.Remove("Column4");
                PartsOnOrders.Columns.Remove("Column5");
            }
            catch (Exception e)
            {
                // Handle Error
            }
            PartsOnOrders.TableName = "PartsOnOrder";
        }
        private void FinalizeOnOrderTables()
        {
            // Add Order Info Tables
            OrderSet.Tables.Add(MaterialsOnOrders);
            OrderSet.Tables.Add(LaborsOnOrders);
            OrderSet.Tables.Add(PartsOnOrders);
        }

        private void GetOrderMaterialInfo(DataTable table)
        {
            // Create the table schema
            MaterialsOnOrders = new DataTable();
            MaterialsOnOrders.Columns.Add(new DataColumn() { Caption = "Order", DataType = typeof(string) });
            MaterialsOnOrders.Columns.Add(new DataColumn() { Caption = "MaterialPN", DataType = typeof(string) });
            MaterialsOnOrders.Columns.Add(new DataColumn() { Caption = "Amount", DataType = typeof(int) });
            MaterialsOnOrders.Columns.Add(new DataColumn() { Caption = "LineType", DataType = typeof(string) });
            MaterialsOnOrders.Columns.Add(new DataColumn() { Caption = "AccumarkFabCode", DataType = typeof(string) });

            // Loop through the new unique table of orders
            foreach (var order in Orders.AsEnumerable())
            {
                var partsOnOrder = GetMaterialsFromOrder(order, table, order.Field<string>("OrderNumber"));
                MaterialsOnOrders.Merge(partsOnOrder, false, MissingSchemaAction.Add);
            }
            // Remove the first 4 columns that get automatically added
            try
            {
                MaterialsOnOrders.Columns.Remove("Column1");
                MaterialsOnOrders.Columns.Remove("Column2");
                MaterialsOnOrders.Columns.Remove("Column3");
                MaterialsOnOrders.Columns.Remove("Column4");
                MaterialsOnOrders.Columns.Remove("Column5");
            }
            catch (Exception e)
            {
                // Handle Error
            }
            MaterialsOnOrders.TableName = "MaterialsOnOrder";
        }
        private void GetOrderLaborInfo(DataTable table)
        {
            // Create the table schema
            LaborsOnOrders = new DataTable();
            LaborsOnOrders.Columns.Add(new DataColumn() { Caption = "Parent", DataType = typeof(string) });
            LaborsOnOrders.Columns.Add(new DataColumn() { Caption = "SequenceNo", DataType = typeof(string) });
            LaborsOnOrders.Columns.Add(new DataColumn() { Caption = "Desc", DataType = typeof(string) });
            LaborsOnOrders.Columns.Add(new DataColumn() { Caption = "RMSPer", DataType = typeof(string) });
            LaborsOnOrders.Columns.Add(new DataColumn() { Caption = "RLSPer", DataType = typeof(string) });
            LaborsOnOrders.Columns.Add(new DataColumn() { Caption = "RLSPer", DataType = typeof(string) });

            // Loop through the new unique table of orders
            foreach (var order in Orders.AsEnumerable())
            {
                var partsOnOrder = GetLaborsFromOrder(order, table, order.Field<string>("OrderNumber"));
                LaborsOnOrders.Merge(partsOnOrder, false, MissingSchemaAction.Add);
            }
            // Remove the first 4 columns that get automatically added
            try
            {
                LaborsOnOrders.Columns.Remove("Column1");
                LaborsOnOrders.Columns.Remove("Column2");
                LaborsOnOrders.Columns.Remove("Column3");
                LaborsOnOrders.Columns.Remove("Column4");
                LaborsOnOrders.Columns.Remove("Column5");
                LaborsOnOrders.Columns.Remove("Column6");
            }
            catch (Exception e)
            {
                // Handle Error
            }
            LaborsOnOrders.TableName = "LaborsOnOrder";
        }
        #endregion


        static string GetTabCodeFromBOMSearch(string bomSearch)
        {
            if (bomSearch.Contains("~"))
            {
                // Create new datarow with just the 3 columns
                var splitter = bomSearch.Split('~');

                if (splitter.Length >= 4)
                {
                    // Extract only the number from the Amount string
                    var doubleArray = Regex.Split(splitter[3], @"[^0-9\.]+")
                                .Where(c => c != "." && c.Trim() != "");

                    return splitter[5].ToString().Trim();
                }
            }

            return string.Empty;
        }
        static DataRow ProcessBOMSearch(DataTable table, object parent, string bomSearch)
        {
            var ret = table.NewRow();

            if (bomSearch.Contains("~"))
            {
                // Create new datarow with just the 3 columns
                var splitter = bomSearch.Split('~');

                if (splitter.Length >= 4)
                {
                    // Extract only the number from the Amount string
                    var doubleArray = Regex.Split(splitter[3], @"[^0-9\.]+")
                                .Where(c => c != "." && c.Trim() != "");

                    ret = table.NewRow();
                    ret["Parent"] = parent.ToString().Trim();
                    ret["MaterialPN"] = splitter[0].ToString().Trim();
                    ret["Desc1"] = splitter[1].ToString().Trim();
                    ret["Desc2"] = splitter[2].ToString().Trim();
                    ret["AmountPer"] = string.IsNullOrEmpty(doubleArray.FirstOrDefault()) ? "0" : doubleArray.FirstOrDefault();
                    ret["AccumarkFabCode"] = string.IsNullOrEmpty(splitter[4].Trim()) ? 1 : double.Parse(splitter[4].ToString().Trim());
                }
            }
            else ret = null;

            return ret;
        }
        private DataTable CreateNewMaterialTable()
        {
            var table = new DataTable();
            table.Columns.Add(new DataColumn() { ColumnName = "Parent", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "MaterialPN", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Desc1", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Desc2", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "AmountPer", DataType = typeof(double) });
            table.Columns.Add(new DataColumn() { ColumnName = "AccumarkFabCode", DataType = typeof(int) });
            return table;
        }
        private DataTable GetPartsFromOrder(DataRow orderRow, DataTable OrigTable, string OrderNumber)
        {
            var _order = orderRow.Field<string>("OrderNumber"); // Current Order Number

            // Get all the parts of the current order
            var orderParts = OrigTable.AsEnumerable()
                .Where(w => !string.IsNullOrEmpty(w.Field<object>("WONum").ToString()) 
                    && double.Parse(w.Field<object>("WONum").ToString()) == double.Parse(_order)
                    && !string.IsNullOrEmpty(w.Field<object>("Material").ToString()))
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("PN") ? x.Field<object>("PN").ToString() : string.Empty,
                    Qty = x.Table.Columns.Contains("Qty") ? Convert.ToInt32(x.Field<object>("Qty").ToString()) : 0,
                    Route = x.Table.Columns.Contains("LaborDesc") ? x.Field<object>("LaborDesc").ToString() : "none"
                });

            // Group the parts by part number and route
            var orderPartsSummed = orderParts.GroupBy(g => new
            {
                PN = g.PN,
                Route = g.Route
            })
                .Select(s => new
                {
                    OrderNumber = OrderNumber,
                    PN = s.Key.PN,
                    Qty = s.FirstOrDefault().Qty
                }).Distinct();

            return orderPartsSummed.ToList().ToTable();
        }
        private DataTable GetMaterialsFromOrder(DataRow orderRow, DataTable OrigTable, string OrderNumber)
        {
            var _order = orderRow.Field<string>("OrderNumber"); // Current Order Number

            // Get all the parts of the current order
            var orderParts = OrigTable.AsEnumerable()
                .Where(w => !string.IsNullOrEmpty(w.Field<object>("WONum").ToString()) 
                && double.Parse(w.Field<object>("WONum").ToString()) == double.Parse(_order)
                && !string.IsNullOrEmpty(w.Field<string>("Material")))
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("PN") ? x.Field<object>("PN").ToString() : string.Empty,
                    Qty = x.Table.Columns.Contains("Qty") ? Convert.ToInt32(x.Field<object>("Qty").ToString()) : 0,
                    Route = x.Table.Columns.Contains("LaborDesc") ? x.Field<object>("LaborDesc").ToString() : "none",
                    TabCode = GetTabCodeFromBOMSearch(x.Field<string>("Material").ToString())
                });

            // Group the parts by part number and sum the quantity
            var orderPartsSummed = orderParts.GroupBy(g => new
            {
                PN = g.PN,
                Route = g.Route,
                TabCode = g.TabCode
            })
                .Select(s => new
                {
                    PN = s.Key.PN,
                    Route = s.Key.Route,
                    Qty = s.FirstOrDefault().Qty,
                    AccumarkTabCode = s.Key.TabCode
                });

            // Find each material and sum(qty) * AmountPer
            var allMaterials = new List<dynamic>();
            foreach (var part in orderPartsSummed)
            {
                var materialPartsSumed = Materials.AsEnumerable()
                    .Where(w => w.Field<object>("Parent").ToString() == part.PN)
                    .Select(s => new
                    {
                        Route = part.Route,
                        Parent = s.Field<object>("Parent").ToString(),
                        MaterialPN = s.Field<object>("MaterialPN").ToString(),
                        Amount = Math.Round(Convert.ToDecimal(s.Field<object>("AmountPer").ToString()) * part.Qty, 3),
                        AccumarkTabCode = part.AccumarkTabCode
                    });

                allMaterials.AddRange(materialPartsSumed);
            }

            // Group by Material
            var orderMaterialsComplete = allMaterials.GroupBy(g => new
            {
                Parent = g.Parent,
                Route = g.Route,
                MaterialPN = g.MaterialPN,
                AccumarkTabCode = g.AccumarkTabCode
            })
                .Select(s => new
                {
                    OrderNumber = OrderNumber,
                    PN = s.Key.Parent,
                    MaterialPN = s.Key.MaterialPN.ToString().Trim(),
                    Amount = s.Sum(a => (decimal)a.Amount),
                    AccumarkTabCode = s.Key.AccumarkTabCode
                }).Distinct();

            return orderMaterialsComplete.ToList().ToTable();
        }
        private DataTable GetLaborsFromOrder(DataRow orderRow, DataTable OrigTable, string OrderNumber)
        {
            var _order = orderRow.Field<string>("OrderNumber"); // Current Order Number

            // Get all the parts of the current order
            var orderParts = OrigTable.AsEnumerable()
                .Where(w =>!string.IsNullOrEmpty(w.Field<object>("WONum").ToString()) 
                    && double.Parse(w.Field<object>("WONum").ToString()) == double.Parse(_order))
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("PN") ? x.Field<object>("PN").ToString() : string.Empty,
                    Qty = x.Table.Columns.Contains("Qty") ? Convert.ToInt32(x.Field<object>("Qty").ToString()) : 0,
                    Route = x.Table.Columns.Contains("LaborDesc") ? x.Field<object>("LaborDesc").ToString() : "0"
                });

            // Group the parts by part number and sum the quantity
            var orderPartsSummed = orderParts.GroupBy(g => new
            {
                PN = g.PN,
                Route = g.Route
            })
                .Select(s => new
                {
                    PN = s.Key.PN,
                    Route = s.Key.Route,
                    Qty = s.FirstOrDefault().Qty
                });

            // Find each material and sum(qty) * AmountPer
            var allLabors = new List<dynamic>();
            foreach (var part in orderPartsSummed)
            {
                var LaborPartsSumed = Labors.AsEnumerable()
                    .Where(w => w.Field<object>("Parent").ToString() == part.PN && w.Field<object>("Desc").ToString() == part.Route)
                    .Select(s => new
                    {
                        Parent = s.Field<object>("Parent").ToString(),
                        SequenceNo = s.Field<object>("SequenceNo").ToString(),
                        Desc = s.Field<object>("Desc").ToString(),
                        RMSTotal = 0,
                        RLSTotal = Convert.ToDecimal(Math.Round(Convert.ToDouble(s.Field<object>("RLSPer").ToString()) * part.Qty, 3)),
                        SLHSTotal = 0
                    });

                allLabors.AddRange(LaborPartsSumed);
            }

            // Group by Material
            var orderLaborsComplete = allLabors.GroupBy(g => new { SequenceNo = g.SequenceNo, Desc = g.Desc, Parent = g.Parent })
                .Select(s => new
                {
                    OrderNumber = OrderNumber,
                    PN = s.Key.Parent,
                    SequenceNo = s.Key.SequenceNo,
                    Desc = s.Key.Desc,
                    RMSTotal = s.Sum(a => (decimal)a.RMSTotal),
                    RLSTotal = s.Sum(a => (decimal)a.RLSTotal),
                    SLHSTotal = s.Sum(a => (decimal)a.SLHSTotal)
                });

            return orderLaborsComplete.ToList().ToTable();
        }



        
    }
}
