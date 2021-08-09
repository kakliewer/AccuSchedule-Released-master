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

namespace AccuSchedule.UI.Plugins.Tools
{
    public class FilterOrdersTool : ToolPlugin
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
        private enum TableToFilterEnum
        {
            Function = 1
        }

        private static string[] TableToFilterList
        {
            get
            {
                // Conver the enum to a list of strings
                var ret = Enum.GetValues(typeof(TableToFilterEnum)).Cast<object>().Select(s => s.ToString()).ToArray();

                AddObjectToInject(ret); // Set the objects to inject
                return ret;
            }
        }

        private enum FilterByEnum
        {
            CustomerFunc = 1,
            OrderFunc = 3,
            PartFunc = 4,
            MaterialFunc = 5
        }


        private static string[] FilterByList
        {
            get
            {
                // Conver the enum to a list of strings
                var ret = Enum.GetValues(typeof(FilterByEnum)).Cast<object>().Select(s => s.ToString()).ToArray();

                AddObjectToInject(ret); // Set the objects to inject
                return ret;
            }
        }
        #endregion


        // Main Function
        public DataSet Filter(DataSet _set, Action<DataSet> FilterItems, string _Combo_TableNameOrFunction_TableToFilterList, string _Combo_ColumnNameOrFunc_FilterByList, string[] FilterValues)
        {
            if (_set == null) return _set;
            if (!string.IsNullOrEmpty(_Combo_TableNameOrFunction_TableToFilterList) || !string.IsNullOrEmpty(_Combo_ColumnNameOrFunc_FilterByList) || FilterValues != null && FilterValues.Length > 0)
            {
                // Find Tables
                DataTable filterTable = _Combo_TableNameOrFunction_TableToFilterList != TableToFilterEnum.Function.ToString() ? _set.Tables[_Combo_TableNameOrFunction_TableToFilterList] : null;

                // Create new GroupedOrders Table
                FilterBy(_set, filterTable, _Combo_TableNameOrFunction_TableToFilterList, _Combo_ColumnNameOrFunc_FilterByList, FilterValues);
            }

            return _set;
        }
        

        private bool FilterBy(DataSet _set, DataTable TableToFilter, string tableOrFunction, string ColumnOrFunction, string[] FilterValues)
        {
            // Try to conver the enum values to do checks cleaner
            _ = Enum.TryParse(tableOrFunction, out TableToFilterEnum filterByTableEnumValue);
            _ = Enum.TryParse(ColumnOrFunction, out FilterByEnum filterByColEnumValue);


            // Check if we should run a function
            bool isFunction = false;
            if (filterByColEnumValue != 0 && filterByTableEnumValue == TableToFilterEnum.Function)
                   isFunction = true;


            var filteredItems = new List<DataRow>();
            if (TableToFilter != null && !isFunction)
            { // Process filtering to a single table
                var rowsToRemove = TableToFilter.RemoveRowsFromTableContainingValue(FilterValues, new string[] { ColumnOrFunction });
                if (rowsToRemove != null && rowsToRemove.Any()) filteredItems.AddRange(rowsToRemove);
            } else if (isFunction)
            { // Process filtering as a function that effects multiple known tables

                switch (filterByColEnumValue)
                {
                    case FilterByEnum.CustomerFunc: // Filter by Customer or CustomerNumber
                        FilterFunctionByCustomer(_set, FilterValues);
                        break;
                    case FilterByEnum.OrderFunc: // Filter by OrderNumber, Date, Opt1, Opt2
                        FilterFunctionByOrder(_set, FilterValues);
                        break;
                    case FilterByEnum.PartFunc: // Filter by Part# or Part Desc, Greater or Less than
                        FilterFunctionByPart(_set, FilterValues);
                        break;
                    case FilterByEnum.MaterialFunc:
                        FilterFunctionByMaterial(_set, FilterValues);
                        break;
                    default:
                        break;
                }

            }

            
            return true;
        }

        #region FilterLogic
        private void FilterFunctionByCustomer(DataSet _set, string[] FilterValues)
        {
            var filteredOrders = FilterOrdersTableForCustomer(_set, FilterValues);
            UpdateOnOrderTablesFromOrderTable(_set, filteredOrders);
        }
        private IEnumerable<DataRow> FilterOrdersTableForCustomer(DataSet _set, string[] FilterValues)
        {
            // Filter by Customer
            DataTable orderTable = _set.Tables["Orders"];
            DataTable customerTable = _set.Tables["Customers"];

            // Find rows with selected values in the Customers table
            var colsToSearch = new string[] { "Customer", "CustomerNumber" };
            var Filtered = customerTable.FindRowsInDataTable(FilterValues, colsToSearch);

            // Find the rows in the orders table with corresponding Customer

            var orderColsToSearch = new string[] { "Customer" };
            var filteredOrders = Filtered.RemoveRowsFromTableContainingValue("Customer", orderColsToSearch, orderTable, false, true);

            return filteredOrders;
        }
        
        private void FilterFunctionByOrder(DataSet _set, string[] FilterValues)
        {
            var filteredOrders = FilterOrdersTableForOrders(_set, FilterValues);
            UpdateOnOrderTablesFromOrderTable(_set, filteredOrders);
        }
        private IEnumerable<DataRow> FilterOrdersTableForOrders(DataSet _set, string[] FilterValues)
        {
            // Filter by Customer
            DataTable orderTable = _set.Tables["Orders"];

            // Find rows with selected values in the Customers table
            var colsToSearch = new string[] { "OrderNumber", "DateRequested", "Opt1", "Opt2" };
            var Filtered = orderTable.RemoveRowsFromTableContainingValue(FilterValues, colsToSearch);

            return Filtered;
        }

        private void FilterFunctionByPart(DataSet _set, string[] FilterValues)
        {
            var filteredOrders = FilterPartsOnOrderTable(_set, FilterValues);
            UpdateOnOrderTablesFromOrderTable(_set, filteredOrders);
        }
        private IEnumerable<DataRow> FilterPartsOnOrderTable(DataSet _set, string[] FilterValues)
        {
            // Filter by Customer
            DataTable ordersTable = _set.Tables["Orders"];
            DataTable partsOnOrderTable = _set.Tables["PartsOnOrder"];
            DataTable partsTable = _set.Tables["Parts"];

            // Find rows with selected values in the Customers table
            var colsToSearch = new string[] { "PN", "Desc1", "Desc2" };
            var Filtered = partsTable.FindRowsInDataTable(FilterValues, colsToSearch);

            // Find the rows in the orders table with corresponding Customer
            var partColsToSearch = new string[] { "PN" };
            var filteredParts = Filtered.RemoveRowsFromTableContainingValue("PN", partColsToSearch, partsOnOrderTable, false, true);

            // Check if FilteredItems OrderNumbers still exist in OnOrder, if it doesn't then remove from Orders table
            var allFoundOrders = new List<DataRow>();
            var filteredOrderNumbers = filteredParts.GroupBy(g => g.Field<object>("OrderNumber").ToString()).Select(s => s.Key);
            foreach (var orderNum in filteredOrderNumbers)
            {
                var foundOrderNumbers = partsOnOrderTable.FindRowsInDataTable(new string[] { orderNum }, new string[] { "OrderNumber" }, false, true);
                if (!foundOrderNumbers.Any()) // Doesn't exist, remove from order table
                    allFoundOrders.AddRange(ordersTable.RemoveRowsFromTableContainingValue(new string[] { orderNum }, new string[] { "OrderNumber" }, false, true));
            }

            return allFoundOrders;
        }

        private void FilterFunctionByMaterial(DataSet _set, string[] FilterValues)
        {
            var filteredOrders = FilterMaterialsOnOrderTable(_set, FilterValues);
            UpdateOnOrderTablesFromOrderTable(_set, filteredOrders);
        }
        private IEnumerable<DataRow> FilterMaterialsOnOrderTable(DataSet _set, string[] FilterValues)
        {
            // Filter by Customer
            DataTable ordersTable = _set.Tables["Orders"];
            DataTable materialsOnOrderTable = _set.Tables["MaterialsOnOrder"];
            DataTable materialsTable = _set.Tables["Materials"];

            // Find rows with selected values in the Customers table
            var colsToSearch = new string[] { "MaterialPN", "Desc1", "Desc2" };
            var Filtered = materialsTable.FindRowsInDataTable(FilterValues, colsToSearch);

            // Find the rows in the orders table with corresponding Customer
            var matColsToSearch = new string[] { "MaterialPN" };
            var filteredParts = Filtered.RemoveRowsFromTableContainingValue("MaterialPN", matColsToSearch, materialsOnOrderTable, false, true);

            // Check if FilteredItems OrderNumbers still exist in OnOrder, if it doesn't then remove from Orders table
            var allFoundOrders = new List<DataRow>();
            var filteredOrderNumbers = filteredParts.GroupBy(g => g.Field<object>("OrderNumber").ToString()).Select(s => s.Key);
            foreach (var orderNum in filteredOrderNumbers)
            {
                var foundOrderNumbers = materialsOnOrderTable.FindRowsInDataTable(new string[] { orderNum }, new string[] { "OrderNumber" }, false, true);
                if (!foundOrderNumbers.Any()) // Doesn't exist, remove from order table
                    allFoundOrders.AddRange(ordersTable.RemoveRowsFromTableContainingValue(new string[] { orderNum }, new string[] { "OrderNumber" }, false, true));
            }

            return allFoundOrders;
        }
        #endregion


        #region Helpers
        private void UpdateOnOrderTablesFromOrderTable(DataSet _set, IEnumerable<DataRow> filteredOrders)
        {
            if (filteredOrders == null || !filteredOrders.Any()) return;

            DataTable partsOnOrderTable = _set.Tables["PartsOnOrder"];
            DataTable materialsTable = _set.Tables["MaterialsOnOrder"];
            DataTable laborsTable = _set.Tables["LaborsOnOrder"];

            // Remove each order row from the tables
            foreach (var orderToRemove in filteredOrders)
            {
                var orderNum = orderToRemove.Field<object>("OrderNumber").ToString();

                // Remove any value where ordernumber is present
                var partsOnOrderRowsRemoved = partsOnOrderTable.RemoveRowsFromTableContainingValue(new string[] { orderNum }, new string[] { "OrderNumber" }, false, true);
                var materialsOnOrderRowsRemoved = materialsTable.RemoveRowsFromTableContainingValue(new string[] { orderNum }, new string[] { "OrderNumber" }, false, true);
                var laborsOnOrderRowsRemoved = laborsTable.RemoveRowsFromTableContainingValue(new string[] { orderNum }, new string[] { "OrderNumber" }, false, true);
            }
        }
        #endregion







    }
}
