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

namespace AccuSchedule.UI.Plugins.Tools
{
    public class ScrubberTool : ToolPlugin
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


        public DataSet ScrubJDEQuery(DataTable _table)
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

        // Main Tables
        private DataTable Orders { get; set; }
        private DataTable Customers { get; set; }
        private DataTable Parts { get; set; }
        private DataTable Labors { get; set; }
        private DataTable Materials { get; set; }

        // Order Information
        private DataTable PartsOnOrders { get; set; }
        private DataTable MaterialsOnOrders { get; set; }
        private DataTable LaborsOnOrders { get; set; }

        // Relational Tables
        private DataTable PartsCustomer { get; set; }

        #region DataSet
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
        #endregion

        #region Populate Main Tables
        private void GetAllOrders(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    Customer = x.Table.Columns.Contains("NameAlpha") ? x.Field<object>("NameAlpha").ToString() : string.Empty,
                    OrderNumber = x.Table.Columns.Contains("DocumentOrderInvoiceE") ? Convert.ToString(x.Field<object>("DocumentOrderInvoiceE").ToString()) : "0",
                    DateRequested = x.Table.Columns.Contains("Date Requested") ? x.Field<object>("Date Requested").ToString() : x.Table.Columns.Contains("DateRequested") ? x.Field<object>("DateRequested").ToString() : DateTime.Now.ToShortDateString().ToString(),
                    Opt1 = x.Table.Columns.Contains("Body") ? x.Field<object>("Body").ToString() : string.Empty,
                    Opt2 = x.Table.Columns.Contains("Line_Set") && x.Table.Columns.Contains("Job_Seq") && x.Table.Columns.Contains("Job") 
                    ? CombineOpt2(x.Field<object>("Line_Set").ToString(), x.Field<object>("Job_Seq").ToString(), x.Field<object>("Job").ToString()) 
                    : null,
                    StatusCodeLast = x.Table.Columns.Contains("StatusCodeLast") ? x.Field<object>("StatusCodeLast").ToString() : "0",
                    StatusCodeNext = x.Table.Columns.Contains("StatusCodeNext") ? x.Field<object>("StatusCodeNext").ToString() : "0"
                }).Distinct().ToList();

            Orders = query.ToTable();
            Orders.TableName = "Orders";
        }
        private void GetAllParts(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("Identifier2ndItem") ? x.Field<object>("Identifier2ndItem").ToString() : string.Empty,
                    Desc1 = x.Table.Columns.Contains("DescriptionLine1") ? x.Field<object>("DescriptionLine1").ToString() : string.Empty,
                    Desc2 = x.Table.Columns.Contains("DescriptionLine2") ? x.Field<object>("DescriptionLine2").ToString() : string.Empty
                }).Distinct().OrderBy(o => o.PN).ToList();

            Parts = query.ToTable();
            
            Parts.TableName = "Parts";
        }
        private void GetAllCustomers(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    Customer = x.Table.Columns.Contains("NameAlpha") 
                    ? x.Field<object>("NameAlpha").ToString() 
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
                    Parent = x.Table.Columns.Contains("Identifier2ndItem") ? x.Field<object>("Identifier2ndItem").ToString() : string.Empty,
                    SequenceNo = x.Table.Columns.Contains("SequenceNoOperations") ? x.Field<object>("SequenceNoOperations").ToString() : "0",
                    Desc = x.Table.Columns.Contains("DescriptionLine101") ? x.Field<object>("DescriptionLine101").ToString() : string.Empty,
                    RMSPer = x.Table.Columns.Contains("RMS_Per") ? x.Field<object>("RMS_Per").ToString() : "0",
                    RLSPer = x.Table.Columns.Contains("RLS_Per") ? x.Field<object>("RLS_Per").ToString() : "0",
                    SLHSPer = x.Table.Columns.Contains("SLHS_Per") ? x.Field<object>("SLHS_Per").ToString() : "0",
                }).Distinct().OrderBy(o => o.Parent).ToList();

            Labors = query.ToTable();
            Labors.TableName = "Labors";
        }
        private void GetAllMaterials(DataTable table)
        {
            var query = table.AsEnumerable()
                .Select(x => new
                {
                    Parent = x.Table.Columns.Contains("Identifier2ndItem") ? x.Field<object>("Identifier2ndItem").ToString() : string.Empty,
                    BOMSearch = x.Table.Columns.Contains("BOM_Search") ? FilterMaterial(x.Field<object>("BOM_Search").ToString()) : string.Empty
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
            
            // Relationship from Parts On Order and Orders
            //OrderSet.Relations.Add("PartsOrders",
            //  OrderSet.Tables["PartsOnOrder"].Columns["Order"],
            //  OrderSet.Tables["Orders"].Columns["OrderNumber"]);

            // Relationship from Parts On Order and Parts
            //OrderSet.Relations.Add("PartsPart",
            //  OrderSet.Tables["PartsOnOrder"].Columns["Part"],
            //  OrderSet.Tables["Parts"].Columns["PN"]);
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



        #region Helpers
        static string CombineOpt2(string val1, string val2, string val3)
        {
            var ret = string.Empty;

            if (!string.IsNullOrEmpty(val1) && !string.IsNullOrEmpty(val1) && !string.IsNullOrEmpty(val1)) 
                ret = string.Format("{0}, {1}, {2}", val1, val2, val3);

            return ret;
        }
        static string FilterMaterial(string value) => value.Contains("THIS ROW REPRESENTS ALL CHILD") ? string.Empty : value;
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
        static decimal GetMatFromBOMSearch(string bomSearch)
        {
            if (bomSearch.Contains("~"))
            {
                // Create new datarow with just the 3 columns
                var splitter = bomSearch.Split('~');

                if (splitter.Length >= 3)
                {
                    // Extract only the number from the Amount string
                    var doubleArray = Regex.Split(splitter[3], @"[^0-9\.]+")
                                .Where(c => c != "." && c.Trim() != "");
                    return doubleArray.Any() ? Convert.ToDecimal(doubleArray.FirstOrDefault()) : 0;
                }
            }

            return 0;
        }

        // Table Helpers
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
                .Where(w => double.Parse(w.Field<object>("DocumentOrderInvoiceE").ToString()) == double.Parse(_order) 
                && !string.IsNullOrEmpty(FilterMaterial(w.Field<object>("BOM_Search").ToString())))
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("Identifier2ndItem") ? x.Field<object>("Identifier2ndItem").ToString() : string.Empty,
                    Qty = x.Table.Columns.Contains("UnitsPrimaryQtyOrder") ? Convert.ToInt32(x.Field<object>("UnitsPrimaryQtyOrder").ToString()) : 0,
                    LineType = x.Table.Columns.Contains("LineType") ? x.Field<string>("LineType") : string.Empty,
                    Route = x.Table.Columns.Contains("DescriptionLine101") ? x.Field<object>("DescriptionLine101").ToString() : "none"
                });

            // Group the parts by part number and route
            var orderPartsSummed = orderParts.GroupBy(g => new
            {
                PN = g.PN,
                Route = g.Route
            })
                .Select(s => new
                {
                    PN = s.Key.PN,
                    Qty = s.FirstOrDefault().Qty
                }).Distinct();

            // Add back in the LineTypes
            var orderPartsComplete = orderPartsSummed.Select(s =>
            {
                var lineType = orderParts.Where(w => w.PN == s.PN).FirstOrDefault().LineType;

                return new 
                {
                    OrderNumber = OrderNumber,
                    PN = s.PN,
                    Qty = s.Qty,
                    LineType = lineType
                };
            });

            return orderPartsComplete.ToList().ToTable();
        }
        private DataTable GetMaterialsFromOrder(DataRow orderRow, DataTable OrigTable, string OrderNumber)
        {
            var _order = orderRow.Field<string>("OrderNumber"); // Current Order Number

            // Get all the parts of the current order
            var orderParts = OrigTable.AsEnumerable()
                .Where(w => double.Parse(w.Field<object>("DocumentOrderInvoiceE").ToString()) == double.Parse(_order)
                && !string.IsNullOrEmpty(FilterMaterial(w.Field<string>("BOM_Search"))))
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("Identifier2ndItem") ? x.Field<object>("Identifier2ndItem").ToString() : string.Empty,
                    Qty = x.Table.Columns.Contains("UnitsPrimaryQtyOrder") ? Convert.ToInt32(x.Field<object>("UnitsPrimaryQtyOrder").ToString()) : 0,
                    LineType = x.Table.Columns.Contains("LineType") ? x.Field<string>("LineType") : string.Empty,
                    Route = x.Table.Columns.Contains("DescriptionLine101") ? x.Field<object>("DescriptionLine101").ToString() : "none",
                    TabCode = x.Table.Columns.Contains("BOM_Search") ? GetTabCodeFromBOMSearch(FilterMaterial(x.Field<string>("BOM_Search"))) : "a"
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
                .Where(w => double.Parse(w.Field<object>("DocumentOrderInvoiceE").ToString()) == double.Parse(_order)
                && !string.IsNullOrEmpty(FilterMaterial(w.Field<string>("BOM_Search"))))
                .Select(x => new
                {
                    PN = x.Table.Columns.Contains("Identifier2ndItem") ? x.Field<object>("Identifier2ndItem").ToString() : string.Empty,
                    Qty = x.Table.Columns.Contains("UnitsPrimaryQtyOrder") ? Convert.ToInt32(x.Field<object>("UnitsPrimaryQtyOrder").ToString()) : 0,
                    LineType = x.Table.Columns.Contains("LineType") ? x.Field<string>("LineType") : string.Empty,
                    Route = x.Table.Columns.Contains("DescriptionLine101") ? x.Field<object>("DescriptionLine101").ToString() : "0"
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
                        RMSTotal = Convert.ToDecimal(Math.Round(Convert.ToDouble(s.Field<object>("RMSPer").ToString()) * part.Qty, 3)),
                        RLSTotal = Convert.ToDecimal(Math.Round(Convert.ToDouble(s.Field<object>("RLSPer").ToString()) * part.Qty, 3)),
                        SLHSTotal = Convert.ToDecimal(Math.Round(Convert.ToDouble(s.Field<object>("SLHSPer").ToString()) * part.Qty, 3))
                    });

                allLabors.AddRange(LaborPartsSumed);
            }

            // Group by Material
            var orderLaborsComplete = allLabors.GroupBy(g => new { SequenceNo = g.SequenceNo, Desc = g.Desc, Parent = g.Parent } )
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
        #endregion
    }
}
