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
using System.Dynamic;
using System.Linq.Dynamic;
using AccuSchedule.UI.Methods;
using DataTableExtensions = AccuSchedule.UI.Extensions.DataTableExtensions;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class GroupOrdersTool : ToolPlugin
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
        #endregion


        #region Injections
        private enum GroupingsEnum
        {
            OptionalFields = 1,
            MaterialType = 2,
            Customer = 3,
            SameDay = 4,
            LaborRoute = 5,
            PartNumber = 6
        }

        

        private static string[] GroupList 
        { 
            get 
            {
                // Conver the enum to a list of strings
                var ret = Enum.GetValues(typeof(GroupingsEnum)).Cast<object>().Select(s => s.ToString()).ToArray();

                AddObjectToInject(ret); // Set the objects to inject
                return ret;
            } 
        }
        #endregion


        // Main Grouping Function
        public DataSet Group(DataSet _set, Action<DataSet> GroupItems, string _Combo_GroupBy_GroupList)
        {
            var TablePrefix = string.Empty; // Was used to create new table but not needed.
            if (!string.IsNullOrEmpty(_Combo_GroupBy_GroupList) && _set != null)
            {
                // Get the grouping method
                _ = Enum.TryParse(_Combo_GroupBy_GroupList, out GroupingsEnum enumValue);

                // Find Tables
                DataTable orderTable = _set.Tables["Orders"];
                DataTable partsOnOrderTable = _set.Tables["PartsOnOrder"];
                DataTable materialsTable = _set.Tables["MaterialsOnOrder"];
                DataTable laborsTable = _set.Tables["LaborsOnOrder"];

                // Create new GroupedOrders Table
                GroupBy(_set, TablePrefix, orderTable, partsOnOrderTable, materialsTable, laborsTable, enumValue);

                UpdateSet(_set);
            }

            return _set;
        }


        // Main Tables
        private DataTable GroupedOrders { get; set; }
        private DataTable GroupedParts { get; set; }
        private DataTable GroupedMaterials { get; set; }
        private DataTable GroupedLabors { get; set; }


        #region Grouping Logic
        private bool GroupBy(DataSet _set, string TablePrefix, DataTable orderTable, DataTable partsOnOrderTable, DataTable materialsTable, DataTable laborsTable, GroupingsEnum groupMethod)
        {
            if (groupMethod == 0) return false; // exits as groupmethod was not recongized

            string groupID = string.Format("{0}GroupID{1}", groupMethod, GetGroupIDNum(orderTable, groupMethod.ToString()) > 0 ? GetGroupIDNum(orderTable, groupMethod.ToString()).ToString() : string.Empty);

            // Select Grouping method
            switch (groupMethod)
            {
                case GroupingsEnum.OptionalFields:
                    if (orderTable != null)
                    {
                        OptionalFields_CreateOrdersTable(orderTable, TablePrefix, groupID);
                        GroupedLabors = DataTableExtensions.CopyNewGroupIDsToAnotherTable(laborsTable, GroupedOrders, groupID, new string[] { "OrderNumber" });
                        GroupedMaterials = DataTableExtensions.CopyNewGroupIDsToAnotherTable(materialsTable, GroupedOrders, groupID, new string[] { "OrderNumber" });
                    }
                    break;
                case GroupingsEnum.MaterialType:
                    if (orderTable != null && materialsTable != null)
                    {
                        Material_CreateOrdersTable(orderTable, materialsTable, TablePrefix, groupID);
                        GroupedParts = DataTableExtensions.CopyNewGroupIDsToAnotherTable(partsOnOrderTable, GroupedMaterials, groupID, new string[] { "OrderNumber", "PN" });
                    }
                    break;
                case GroupingsEnum.Customer:
                    if (orderTable != null)
                    {
                        Customer_CreateOrdersTable(orderTable, TablePrefix, groupID);
                        GroupedLabors = DataTableExtensions.CopyNewGroupIDsToAnotherTable(laborsTable, GroupedOrders, groupID, new string[] { "OrderNumber" });
                        GroupedMaterials = DataTableExtensions.CopyNewGroupIDsToAnotherTable(materialsTable, GroupedOrders, groupID, new string[] { "OrderNumber" });
                    }
                    break;
                case GroupingsEnum.SameDay:
                    if (orderTable != null)
                    {
                        SameDay_CreateOrdersTable(orderTable, TablePrefix, groupID);
                        GroupedLabors = DataTableExtensions.CopyNewGroupIDsToAnotherTable(laborsTable, GroupedOrders, groupID, new string[] { "OrderNumber" });
                        GroupedMaterials = DataTableExtensions.CopyNewGroupIDsToAnotherTable(materialsTable, GroupedOrders, groupID, new string[] { "OrderNumber" });
                    }
                    break;
                case GroupingsEnum.LaborRoute:
                    if (orderTable != null && laborsTable != null)
                    {
                        Labor_CreateOrdersTable(orderTable, laborsTable, TablePrefix, groupID);
                        //GroupedMaterials = DataTableExtensions.CopyNewGroupIDsToAnotherTable(materialsTable, GroupedLabors, groupID, new string[] { "OrderNumber" });
                        GroupedParts = DataTableExtensions.CopyNewGroupIDsToAnotherTable(partsOnOrderTable, GroupedLabors, groupID, new string[] { "OrderNumber" });
                    }
                    break;
                case GroupingsEnum.PartNumber:
                    if (orderTable != null && partsOnOrderTable != null)
                    {
                        Parts_CreateOrdersTable(orderTable, partsOnOrderTable, TablePrefix, groupID);
                        //GroupedParts = CopyNewGroupIDsToAnotherTable(partsOnOrderTable, GroupedMaterials, groupID, new string[] { "OrderNumber", "PN" });
                    }
                    break;
                default:
                    break;
            }

            // Add entries to this list to ignore processing the parts list
            var ignorePartsProcessing = new GroupingsEnum[] 
            { 
                GroupingsEnum.LaborRoute 
            };

            if (orderTable != null && partsOnOrderTable != null && !ignorePartsProcessing.Contains(groupMethod) && groupMethod != GroupingsEnum.MaterialType && groupMethod != GroupingsEnum.LaborRoute && groupMethod != GroupingsEnum.PartNumber) 
                GetParts(GroupedOrders, partsOnOrderTable, TablePrefix, groupMethod, groupID);

            return true;
        }

        #endregion


        #region Orders
        private void OptionalFields_CreateOrdersTable(DataTable orderTable, string TablePrefix, string groupIdentifier)
        {
            var query = orderTable.AsEnumerable()
                    .GroupBy(g => new
                    {
                        Opt1 = g.Table.Columns.Contains("Opt1") ? g.Field<string>("Opt1") : string.Empty,
                        Opt2 = g.Table.Columns.Contains("Opt2") ? g.Field<string>("Opt2") : string.Empty
                    });

            // Set the new table
            var tableName = TablePrefix + "Orders";
            GroupedOrders = DataTableExtensions.CopyGroupingToTableWithNewIDColumn(orderTable, query, tableName, groupIdentifier, "OrderNumber");

        }
        private void Customer_CreateOrdersTable(DataTable orderTable, string TablePrefix, string groupIdentifier)
        {
            var query = orderTable.AsEnumerable()
                    .GroupBy(g => new
                    {
                        Customer = g.Table.Columns.Contains("Customer") ? g.Field<string>("Customer") : string.Empty
                    });

            // Set the new table
            var tableName = TablePrefix + "Orders";
            GroupedOrders = DataTableExtensions.CopyGroupingToTableWithNewIDColumn(orderTable, query, tableName, groupIdentifier, "OrderNumber");
        }
        private void SameDay_CreateOrdersTable(DataTable orderTable, string TablePrefix, string groupIdentifier)
        {
            var query = orderTable.AsEnumerable()
                    .GroupBy(g => new
                    {
                        DateRequested = g.Table.Columns.Contains("DateRequested") ? g.Field<string>("DateRequested") : string.Empty
                    });

            // Set the new table
            var tableName = TablePrefix + "Orders";
            GroupedOrders = DataTableExtensions.CopyGroupingToTableWithNewIDColumn(orderTable, query, tableName, groupIdentifier, "OrderNumber");
        }

        private void Material_CreateOrdersTable(DataTable orderTable, DataTable materialsOnOrderTable, string TablePrefix, string groupIdentifier)
        {
            var query = materialsOnOrderTable.AsEnumerable()
                    .GroupBy(g => new
                    {
                        MaterialPN = g.Table.Columns.Contains("MaterialPN") ? g.Field<string>("MaterialPN") : string.Empty,
                        AccumarkTabCode = g.Table.Columns.Contains("AccumarkTabCode") ? g.Field<string>("AccumarkTabCode") : string.Empty
                    });

            // Set the new table
            var tableName = TablePrefix + "MaterialsOnOrder";
            GroupedMaterials = DataTableExtensions.CopyGroupingToTableWithNewIDColumn(materialsOnOrderTable, query, tableName, groupIdentifier, "MaterialPN");

            GroupedOrders = DataTableExtensions.CopyNewGroupIDsToAnotherTable(orderTable, GroupedMaterials, groupIdentifier, new string[] { "OrderNumber" });


        }
        private void Labor_CreateOrdersTable(DataTable orderTable, DataTable laborsOnOrderTable, string TablePrefix, string groupIdentifier)
        {
            var query = laborsOnOrderTable.AsEnumerable()
                    .GroupBy(g => new
                    {
                        SequenceNo = g.Table.Columns.Contains("SequenceNo") ? g.Field<string>("SequenceNo") : string.Empty
                    });

            // Set the new table
            var tableName = TablePrefix + "LaborsOnOrder";
            GroupedLabors = DataTableExtensions.CopyGroupingToTableWithNewIDColumn(laborsOnOrderTable, query, tableName, groupIdentifier, "SequenceNo");

            GroupedOrders = DataTableExtensions.CopyNewGroupIDsToAnotherTable(orderTable, GroupedLabors, groupIdentifier, new string[] { "OrderNumber" });


        }
        private void Parts_CreateOrdersTable(DataTable orderTable, DataTable partsOnOrderTable, string TablePrefix, string groupIdentifier)
        {
            var query = partsOnOrderTable.AsEnumerable()
                    .GroupBy(g => new
                    {
                        PN = g.Table.Columns.Contains("PN") ? g.Field<string>("PN") : string.Empty
                    });

            // Set the new table
            var tableName = TablePrefix + "PartsOnOrder";
            GroupedParts = DataTableExtensions.CopyGroupingToTableWithNewIDColumn(partsOnOrderTable, query, tableName, groupIdentifier, "PN");

            GroupedOrders = DataTableExtensions.CopyNewGroupIDsToAnotherTable(orderTable, GroupedParts, groupIdentifier, new string[] { "OrderNumber" });


        }
        #endregion


        #region Parts
        private bool GetParts(DataTable orderTable, DataTable partsOnOrderTable, string TablePrefix, GroupingsEnum groupMethod, string groupID) =>
            orderTable != null && partsOnOrderTable != null ? CreatePartsOnOrderTable(orderTable, partsOnOrderTable, TablePrefix, groupMethod, groupID) : false;

        private bool CreatePartsOnOrderTable(DataTable orderTable, DataTable partsOnOrderTable, string TablePrefix, GroupingsEnum groupMethod, string groupID)
        {
            List<DataTable> allParts = new List<DataTable>();

            // Find all Orders pointing towards Optionals
            var origOrders = orderTable.AsEnumerable();

            // Find the Parts matching the Orders
            foreach (var order in origOrders)
            {
                // Search the partsonorder table for the order numbers
                var partsOnOrder = partsOnOrderTable.AsEnumerable()
                    .Where(w =>
                        DataTableExtensions.DelimetedStringContains(w.Field<string>("OrderNumber"), order.Field<object>("OrderNumber").ToString()))
                    .ToList();

                // Group the parts by part number and sum the quantity
                var orderGrouped = partsOnOrder.GroupBy(g => new { PN = g.Field<object>("PN") });

                // Group the parts by part number and sum the quantity
                var orderPartsSummed = orderGrouped.Select(s => new
                {
                    PN = s.Key.PN,
                    Qty = s.Sum(x => Convert.ToInt32(x.Field<object>("Qty").ToString())),
                    OrderNumber = string.Join("|", s.Select(w => w.Field<object>("OrderNumber")).Distinct()),
                    LineType = string.Join("|", s.Select(w => w.Field<object>("LineType")).Distinct()),
                    GroupID = order.Field<object>(groupID).ToString(),
                    Row = s.Select(x => x)
                });

                // Add back in the previous GroupID columns
                var ordersList = NewPartsTable(partsOnOrderTable, orderPartsSummed, groupID, "GroupID");

                allParts.Add(ordersList);
                
            }

            
            if (!allParts.Any()) return false;
            GroupedParts = new DataTable();
            GroupedParts.TableName = TablePrefix + "PartsOnOrder";
            allParts.ForEach(f => GroupedParts.Merge(f));
            return true;

        }
        private static DataTable NewPartsTable(DataTable orderTable, IEnumerable<dynamic> orderPartsSummed, string groupID, string searchFor = "GroupID")
        {
            // Initial table with first column as GroupID
            var table = new DataTable();
            table.Columns.Add(new DataColumn() { DataType = typeof(object), ColumnName = groupID }); // Add the current GroupID column to return table

            // If GroupID column exists, then add the value
            var cols = new HashSet<DataColumn>();
            foreach (DataColumn col in orderTable.Columns)
                if (col.ColumnName != searchFor && col.ColumnName.Contains(searchFor))
                    cols.Add(new DataColumn() { DataType = typeof(object), ColumnName = col.ColumnName });

            // Add columns to return table
            table.Columns.AddRange(cols.ToArray()); // Make sure row has columns
            table.Columns.Add(new DataColumn() { DataType = typeof(object), ColumnName = "OrderNumber" });
            table.Columns.Add(new DataColumn() { DataType = typeof(object), ColumnName = "PN" });
            table.Columns.Add(new DataColumn() { DataType = typeof(object), ColumnName = "Qty" });
            table.Columns.Add(new DataColumn() { DataType = typeof(object), ColumnName = "LineType" });

            // Loop through and pass the actual row as a parameter
            var preRet = orderPartsSummed.ToList();
            foreach (var item in preRet)
            {
                var row = table.NewRow();

                // Populate any pre-existing groupings
                var drList = item.Row as IEnumerable<DataRow>;
                if (drList != null)
                {
                    // Convert the dr to a delimeted string
                    var rowDict = new Dictionary<string, object>();
                    foreach (var dr in drList)
                        foreach (DataColumn col in dr.Table.Columns)
                            if (col.ColumnName != searchFor && col.ColumnName != groupID && col.ColumnName.Contains(searchFor))
                                rowDict.Add(col.ColumnName, !string.IsNullOrEmpty(dr.Field<object>(col.ColumnName).ToString()) ? dr.Field<object>(col.ColumnName).ToString() : string.Empty);

                    foreach (var dictRow in rowDict)
                        row[dictRow.Key] = dictRow.Value; // Get the original row value
                }

                row[groupID] = item?.GroupID; // Add Current GroupID
                row["OrderNumber"] = item?.OrderNumber;
                row["PN"] = item?.PN;
                row["Qty"] = item?.Qty;
                row["LineType"] = item?.LineType;
                if (table.ColumnNames().Contains("AccumarkTabCode"))
                    row["AccumarkTabCode"] = item?.AccumarkTabCode;
                table.Rows.Add(row);
            }


            return table;
        }
        #endregion

        #region Helpers
        

        

        
        private int GetGroupIDNum(DataTable table, string groupMethod)
        {
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var col = table.Columns[i];
                if (col.ColumnName.Contains(groupMethod) && col.ColumnName.Contains("GroupID"))
                {
                    var leftOver = col.ColumnName.Replace(string.Format("{0}{1}", groupMethod, "GroupID"), "");
                    var isInt = int.TryParse(leftOver, out int id);

                    if (isInt) id += 1; 
                    else id = 1;

                    return id;
                }
            }
            return 0;
        }
        
        private static bool ColumnExists<columnType>(DataTable table, string ColName)
        {
            foreach (DataColumn dc in table.Columns)
            {
                if (dc.DataType == typeof(columnType) && dc.ColumnName == ColName)
                {
                    return true;
                }
            }

            return false;
        }
        
        private void UpdateSet(DataSet _set)
        {
            _set.AddOrUpdateTable(GroupedOrders);
            _set.AddOrUpdateTable(GroupedParts);
            _set.AddOrUpdateTable(GroupedMaterials);
            _set.AddOrUpdateTable(GroupedLabors);
        }
        #endregion
    }


}
