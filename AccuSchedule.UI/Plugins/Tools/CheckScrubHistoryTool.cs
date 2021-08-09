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
using System.IO;
using AccuSchedule.UI.Methods;
using NCalc.Domain;
using ClosedXML.Excel;
using System.Runtime.Serialization;
using System.Security.Policy;
using AccuSchedule.UI.Plugins.Tools.Views;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using System.Reflection;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class CheckScrubHistoryTool : ToolPlugin
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

        private DataTable HistoryTable { get; set; }

        private DataTable ReturnedItemActions { get; set; }

        public DataSet CheckScrubHistory(DataSet _set, Action<DataSet> Build, string HistoryFile, Button _BrowseForFile)
        {
            
            if (!string.IsNullOrEmpty(HistoryFile))
            {
               
                HistoryTable = CreateHistoryTable("HistoryTable");
                // Populate Current Data into History Table
                var curHistory = CreateHistoryFromCurrent(_set);

                // Load the previous Historical data
                IEnumerable<IXLTable> tables = null;
                if (File.Exists(HistoryFile))
                {
                    ExcelInputHandler ei = new ExcelInputHandler();
                    tables = FindHistoryTables(ei.ExtractTables(HistoryFile, writeAccess: true));
                }
               
                if (tables != null && tables.Any())
                {
                    // Find Historical matches
                    var foundHistory = CheckHistoryBetween(curHistory, tables);
                    var processedDataset = ProcessHistory(curHistory, foundHistory);

                    // Display Action Window
                    if (foundHistory.Any())
                    {
                        var histWin = new HistoryModifications(processedDataset);
                        histWin.Closed += HistWin_Closed;
                        histWin.ShowDialog();
                    } else
                    { // Add the curHistory to xlTable
                        var table = tables.FirstOrDefault();
                        var nTable = table.AsNativeDataTable();
                        
                        curHistory.Merge(nTable, true, MissingSchemaAction.Ignore);

                        var wb = new XLWorkbook();
                        wb.Worksheets.Add(curHistory);
                        wb.SaveAs(HistoryFile);
                    }

                    // Capture window close event to get results
                    if (ReturnedItemActions != null && ReturnedItemActions.AsEnumerable().Any())
                    {
                        
                        // Process Action Window's results
                        ProcessActionResults(_set, ReturnedItemActions);

                        // Save new Entries to history log 
                        SaveCurWOHistory(curHistory, ReturnedItemActions, tables, HistoryFile);
                    }
                }
                else
                {
                    // Create new table with curHistory
                    var wb = new XLWorkbook();
                    wb.Worksheets.Add(curHistory);
                    wb.SaveAs(HistoryFile);
                }
            }

            return _set;
        }

        private void SaveCurWOHistory(DataTable curHistory, DataTable ActionResults, IEnumerable<IXLTable> xlTables, string HistoryFile)
        {
            // Remove 'ignored' actions from curHistory
            foreach (var actionRow in ActionResults.AsEnumerable().Where(w => w.Field<object>("Action").ToString().ToLower().Trim() == "ignore"))
                curHistory.AsEnumerable()
                    .Where(w => w.Field<object>("WONumber").ToString() == actionRow.Field<object>("WONumber").ToString())
                    .ToList()
                    .ForEach(matchedRow => 
                        curHistory.Rows.Remove(matchedRow));
            

            // Append the new history and save the workbook
            var table = xlTables.FirstOrDefault();
            var nTable = table.AsNativeDataTable();
            
            curHistory.Merge(nTable, true, MissingSchemaAction.Ignore);
            var wb = new XLWorkbook();
            wb.Worksheets.Add(curHistory);
            wb.SaveAs(HistoryFile);

        }

        private void ProcessActionResults(DataSet _set, DataTable ActionResults)
        {
            // Get Tables from Set
            var orderTable = _set.Tables.Contains("Orders") ? _set.Tables["Orders"] : null;
            var customerTable = _set.Tables.Contains("Customers") ? _set.Tables["Customers"] : null;
            var partsTable = _set.Tables.Contains("Parts") ? _set.Tables["Parts"] : null;
            var laborTable = _set.Tables.Contains("Labors") ? _set.Tables["Labors"] : null;
            var materialsTable = _set.Tables.Contains("Materials") ? _set.Tables["Materials"] : null;
            var matsOnOrderTable = _set.Tables.Contains("MaterialsOnOrder") ? _set.Tables["MaterialsOnOrder"] : null;
            var laborsOnOrderTable = _set.Tables.Contains("LaborsOnOrder") ? _set.Tables["LaborsOnOrder"] : null;
            var partsOnOrderTable = _set.Tables.Contains("PartsOnOrder") ? _set.Tables["PartsOnOrder"] : null;

            // Modify each row per the Action
            foreach (var actionRow in ActionResults.AsEnumerable())
            {
                var dateLogged = Convert.ToDateTime(actionRow.Field<object>("DateLogged").ToString());
                var dateRequested = Convert.ToDateTime(actionRow.Field<object>("DateRequested").ToString());
                var Customer = actionRow.Field<object>("Customer").ToString();
                var woNumber = actionRow.Field<object>("WONumber").ToString();
                var dateRevised = Convert.ToDateTime(actionRow.Field<object>("DateRevised").ToString());
                var statusCode = actionRow.Field<object>("StatusCode").ToString();
                var pn = actionRow.Field<object>("PN").ToString();
                var qty = Convert.ToInt32(actionRow.Field<object>("Qty").ToString());
                var relatedOrder = actionRow.Field<object>("RelatedOrder").ToString();
                var relatedType = actionRow.Field<object>("RelatedType").ToString();
                var action = actionRow.Field<object>("Action").ToString();

                if (action.ToLower().Trim() == "ignore")
                { // Remove the Order
                    var mLaborsOnOrder = laborsOnOrderTable.AsEnumerable()
                        .Where(w => w.Field<object>("OrderNumber").ToString() == woNumber
                            && w.Field<object>("PN").ToString() == pn).ToList();
                    foreach (var mLaborRow in mLaborsOnOrder) laborsOnOrderTable.Rows.Remove(mLaborRow);

                    var mMaterialsOnOrder = matsOnOrderTable.AsEnumerable()
                        .Where(w => w.Field<object>("OrderNumber").ToString() == woNumber
                            && w.Field<object>("PN").ToString() == pn).ToList();
                    foreach (var mMatRow in mMaterialsOnOrder) matsOnOrderTable.Rows.Remove(mMatRow);

                    var mPartsOnOrder = partsOnOrderTable.AsEnumerable()
                        .Where(w => w.Field<object>("OrderNumber").ToString() == woNumber
                            && w.Field<object>("PN").ToString() == pn).ToList();
                    foreach (var mPartRow in mPartsOnOrder) partsOnOrderTable.Rows.Remove(mPartRow);

                    var mOrder = orderTable.AsEnumerable()
                        .Where(w => w.Field<object>("OrderNumber").ToString() == woNumber).ToList();
                    foreach (var mOrderRow in mOrder) orderTable.Rows.Remove(mOrderRow);

                }
                else if (action.ToLower().Trim() == "continue")
                { // Modify the current rows

                    var mPartsOnOrder = partsOnOrderTable.AsEnumerable()
                        .Where(w => w.Field<object>("OrderNumber").ToString() == woNumber
                            && w.Field<object>("PN").ToString() == pn);

                    foreach (var mPartRow in mPartsOnOrder)
                    {
                        var partQty = Convert.ToInt32(mPartRow.Field<object>("Qty").ToString());

                        if (partQty != (qty)) 
                        {
                            var mLaborsOnOrder = laborsOnOrderTable.AsEnumerable()
                                .Where(w => w.Field<object>("OrderNumber").ToString() == woNumber
                                    && w.Field<object>("PN").ToString() == pn);
                            foreach (var mLaborRow in mLaborsOnOrder)
                            {
                                var seqNo = mLaborRow.Field<object>("SequenceNo").ToString();
                                var labors = laborTable.AsEnumerable()
                                    .FirstOrDefault(w => w.Field<object>("Parent").ToString() == pn
                                    && w.Field<object>("SequenceNo").ToString() == seqNo);

                                if (labors != null) {
                                    mLaborRow.SetField("RLSTotal", Convert.ToDecimal(Math.Round(Convert.ToDouble(labors.Field<object>("RLSPer").ToString()) * qty, 3)));
                                }
                            }

                            var mMaterialsOnOrder = matsOnOrderTable.AsEnumerable()
                                .Where(w => w.Field<object>("OrderNumber").ToString() == woNumber
                                    && w.Field<object>("PN").ToString() == pn);
                            foreach (var mMatRow in mMaterialsOnOrder)
                            {
                                var matNum = mMatRow.Field<object>("MaterialPN").ToString();
                                var materials = materialsTable.AsEnumerable()
                                    .FirstOrDefault(w => w.Field<object>("Parent").ToString() == pn
                                    && w.Field<object>("MaterialPN").ToString() == matNum);

                                if (materials != null)
                                {
                                    mMatRow.SetField("Amount", Convert.ToDecimal(Math.Round(Convert.ToDouble(materials.Field<object>("AmountPer").ToString()) * qty, 3)));
                                }
                            }

                            // Modify part Entry
                            mPartRow.SetField("Qty", qty);

                        }
                    }

                    
                }

                



            }
        }


        private void HistWin_Closed(object sender, EventArgs e)
        {
            var win = sender as HistoryModifications;
            if (win != null)
            {
                var retList = new List<HistoryModifications.BoundList>(win.dgCurrent.ItemsSource as IEnumerable<HistoryModifications.BoundList>);
                ReturnedItemActions = ToDataTable(retList);
            }
        }

        private DataTable ToDataTable<T>(List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);

            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name);
            }
            foreach (T item in items)
            {
                var values = new object[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                {
                    //inserting property values to datatable rows
                    values[i] = Props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }
            //put a breakpoint here and check datatable
            return dataTable;
        }

        private DataSet ProcessHistory(DataTable CurHistory, Dictionary<string, IEnumerable<IXLRangeRow>> History)
        {
            var ret = new DataSet() { DataSetName = "WOHistory" };

            var curTable = CreateHistoryTable("current");
            var histTable = CreateHistoryTable("history");

            var uniqueWOs = History.Select(s => s.Key);

            foreach (var woNum in uniqueWOs)
            {
                // Get the data rows
                var curEntry = CurHistory.AsEnumerable()
                    .Where(w => w.Field<object>("WONumber").ToString() == woNum)
                    .FirstOrDefault();

                var dateReqColNum = curTable.Columns["DateRequested"].Ordinal;

                var lastHistoricalEntry = History[woNum]
                    .OrderByDescending(o => Convert.ToDateTime(o.Cell(dateReqColNum).Value.ToString()));

                // Add the rows to the new tables
                if (curEntry != null && lastHistoricalEntry.Any())
                {
                    AddNativeRow(curEntry, curTable);
                    foreach (var histItem in lastHistoricalEntry)
                        AddIXLRow(histItem, histTable);
                    
                }
            }

            // Finalize the Dataset
            ret.Tables.Add(curTable);
            ret.Tables.Add(histTable);
            ret.Relations.Add("HistoricalMatches", curTable.Columns["WONumber"], histTable.Columns["WONumber"]);

            return ret;
        }


        /// <summary>
        /// Adds new Row to table based on existing row with matching structure.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="toTable"></param>
        private void AddIXLRow(IXLRangeRow row, DataTable toTable)
        {
            // Add to the return table
            var newRow = toTable.NewRow();
            newRow.ItemArray = row.Cells().Select(s => s.Value).ToArray();
            toTable.Rows.Add(newRow);
        }
        private void AddNativeRow(DataRow row, DataTable toTable)
        {
            // Add to the return table
            var newRow = toTable.NewRow();
            newRow.ItemArray = row.ItemArray;
            toTable.Rows.Add(newRow);
        }


        /// <summary>
        /// Checks history from ClosedXML tables against a native DataTable.
        /// </summary>
        /// <param name="toCheck">Native Datatable to compare against. </param>
        /// <param name="againstTables">ClosedXML tables matching the Native Datatable's structure.</param>
        /// <returns>Dictionary with 'WONumber' as Key. Values are rows matching results from ClosedXML.</returns>
        private Dictionary<string, IEnumerable<IXLRangeRow>> CheckHistoryBetween(DataTable toCheck, IEnumerable<IXLTable> againstTables)
        {
            
            var ret = new Dictionary<string, IEnumerable<IXLRangeRow>>();

            // Check against list of WO numbers
            var currentWOs = toCheck.AsEnumerable()
                .Select(s => s.Field<object>("WONumber").ToString())
                .Distinct();

            var tableToCheck = againstTables.First();

            // Get the column letter of the 'WONumber'
            var woNumCol = tableToCheck.HeadersRow().Cells()
                .Where(w => w.Value.ToString() == "WONumber")
                .FirstOrDefault().WorksheetColumn()
                .ColumnLetter();

            foreach (var currentWO in currentWOs)
            {
                // Find any matches to 'currentWO'
                var historicalRowMatches = tableToCheck.Column(woNumCol).Cells()
                    .Where(w => w.Value.ToString() == currentWO)
                    .Select(s => s.Address.RowNumber);

                var historicalRows = new HashSet<IXLRangeRow>();

                if (historicalRowMatches.Any())
                {
                    foreach (var hRow in historicalRowMatches)
                    {
                        if (tableToCheck.Row(hRow) != null)
                            historicalRows.Add(tableToCheck.Row(hRow));
                    }

                    ret.Add(currentWO, historicalRows);
                }

                

            }

            return ret;
        }


        private DataTable CreateHistoryFromCurrent(DataSet _set)
        {
            var ret = HistoryTable.Clone();

            var orderTable = _set.Tables.Contains("Orders") ? _set.Tables["Orders"] : null;
            var customerTable = _set.Tables.Contains("Customers") ? _set.Tables["Customers"] : null;
            var partsTable = _set.Tables.Contains("Parts") ? _set.Tables["Parts"] : null;
            var laborTable = _set.Tables.Contains("Labors") ? _set.Tables["Labors"] : null;
            var materialsTable = _set.Tables.Contains("Materials") ? _set.Tables["Materials"] : null;
            var matsOnOrderTable = _set.Tables.Contains("MaterialsOnOrder") ? _set.Tables["MaterialsOnOrder"] : null;
            var laborsOnOrderTable = _set.Tables.Contains("LaborsOnOrder") ? _set.Tables["LaborsOnOrder"] : null;
            var partsOnOrderTable = _set.Tables.Contains("PartsOnOrder") ? _set.Tables["PartsOnOrder"] : null;


            // Check each order and the parts
            var ordersToCheck = orderTable.AsEnumerable();
            foreach (var order in ordersToCheck)
            {
                var dateLogged = DateTime.Now;
                var orderNumber = order.Field<object>("OrderNumber").ToString();
                var dateRequested = Convert.ToDateTime(order.Field<object>("DateRequested").ToString()).ToShortDateString();
                var dateRevised = Convert.ToDateTime(order.Field<object>("LastStatusChange").ToString()).ToShortDateString();
                var StatusCode = order.Field<object>("WOStatusCode").ToString();
                var RelatedOrderType = order.Field<object>("RelatedOrderType").ToString();
                var RelatedOrderNum = order.Field<object>("RelatedOrderNum").ToString();
                var Customer = order.Field<object>("Customer").ToString();

                // Parts for the Order to Check History against
                var partsToCheck = partsOnOrderTable.AsEnumerable()
                    .Where(w => w.Field<object>("OrderNumber").ToString() == orderNumber)
                    .GroupBy(g => new { OrderNumber = g.Field<object>("OrderNumber").ToString(), PN = g.Field<object>("PN").ToString() })
                    .Select(s => new
                    {
                        OrderNumber = s.Key.OrderNumber,
                        PN = s.Key.PN,
                        Qty = s.Sum(add => long.Parse(add.Field<object>("Qty").ToString()))
                    });

                // Get Related Part Info
                foreach (var part in partsToCheck)
                {
                    // Add to the return table
                    var newRow = ret.NewRow();
                    newRow.SetField("DateLogged", dateLogged);
                    newRow.SetField("DateRequested", dateRequested);
                    newRow.SetField("Customer", Customer);
                    newRow.SetField("WONumber", orderNumber);
                    newRow.SetField("DateRevised", dateRevised);
                    newRow.SetField("StatusCode", StatusCode);
                    newRow.SetField("PN", part.PN);
                    newRow.SetField("Qty", part.Qty);
                    newRow.SetField("RelatedOrder", RelatedOrderNum);
                    newRow.SetField("RelatedType", RelatedOrderType);
                    ret.Rows.Add(newRow);
                }


            }


            return ret;
        }

        private IEnumerable<IXLTable> FindHistoryTables(IEnumerable<IXLTable> tables)
        {
            var ret = new HashSet<IXLTable>();
            foreach (var tbl in tables)
            {
                var bFound = true;
                
                foreach (var col in tbl.HeadersRow()?.Cells())
                {
                    foreach (DataColumn hCol in HistoryTable.Columns)
                    {
                        if (col.Value.ToString().ToLower() == hCol.ColumnName)
                        {
                            bFound = false;
                            break;
                        }
                    }

                    if (!bFound) 
                        break;
                }

                if (bFound)
                    ret.Add(tbl);
            }

            return ret;
        }

        private DataTable CreateHistoryTable(string tableName)
        {
            var ret = new DataTable() { TableName = tableName };

            ret.Columns.Add(new DataColumn() { ColumnName = "DateLogged" });
            ret.Columns.Add(new DataColumn() { ColumnName = "DateRequested" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Customer" });
            ret.Columns.Add(new DataColumn() { ColumnName = "WONumber" });
            ret.Columns.Add(new DataColumn() { ColumnName = "DateRevised" });
            ret.Columns.Add(new DataColumn() { ColumnName = "StatusCode" });
            ret.Columns.Add(new DataColumn() { ColumnName = "PN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Qty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "RelatedOrder" });
            ret.Columns.Add(new DataColumn() { ColumnName = "RelatedType" });

            return ret;
        }


    }
}
