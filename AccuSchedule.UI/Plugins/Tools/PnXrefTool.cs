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
using AccuSchedule.UI.Methods;
using ClosedXML.Excel;
using System.Web;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class PnXrefTool : ToolPlugin
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


        public DataSet PN_CrossReference(DataSet _set, Action<DataSet> GetCrossReferences, string XRef_Location, Button _BrowseForFile = null)
        {
            if (!string.IsNullOrEmpty(XRef_Location))
            {
                ExcelInputHandler ei = new ExcelInputHandler();

                var tables = ei.ExtractTables(XRef_Location);

                foreach (var table in tables)
                    if (IsTableValid(table)) 
                        ProcessXrefTable(_set, table.AsNativeDataTable());
            }

            return _set;
        }

        private void ProcessXrefTable(DataSet _set, DataTable xRefTable)
        {
            // Add in each cross-reference to the Parts table
            DataTable partsTable = _set?.Tables["Parts"];
            if (partsTable == null) return;

            // Loop through the parts table and check for cross-reference
            foreach (var partRow in partsTable.AsEnumerable())
            {
                // Get rows in xRefTable if ColumnName contains "xref"
                var xRefPNName = xRefTable.ColumnNames("part number");
                var xRefMatchedRow = xRefTable.FindRowsInDataTable(new string[] { partRow.Field<object>("PN")?.ToString() }, xRefPNName, false, true).FirstOrDefault();
                if (xRefMatchedRow != null)
                {
                    // Check if value is already in the current partRow.
                    bool addIt = false;
                    var partXRefCols = partsTable.ColumnNames("xref"); // Get the value per each loop incase new columns are added
                    foreach (var partXRefCol in partXRefCols)
                        if (!partRow.DoesValueExistInColumns(partXRefCols, partRow.Field<object>(partXRefCol)?.ToString()))
                        {
                            addIt = true;
                            break;
                        }
                    if (!partXRefCols.Any()) addIt = true;

                    // Add the value
                    if (addIt) 
                        AddCrossReferenceToRow(partRow, xRefMatchedRow);
                }
                
            }
        }

        private void AddCrossReferenceToRow(DataRow partRow, DataRow xRefMatchedRow)
        {
            if (xRefMatchedRow == null) return;

            // Loop through the XRef columns and add the Xref to the partRow
            foreach (var xRefColName in xRefMatchedRow.Table.ColumnNames("xref"))
            {
                var xRefPN = xRefMatchedRow.Field<object>(xRefColName).ToString();
                if (!string.IsNullOrEmpty(xRefPN) && xRefPN.ToLower().Trim() != "n/a")
                {
                    var partXRefTableRows = partRow.Table.ColumnNames("xref");
                    var XRefColumnAvailable = partRow.AnyEmptyValueInColumns(partXRefTableRows);
                    if (!string.IsNullOrEmpty(XRefColumnAvailable))
                    { // Contains Empty value
                        partRow.SetField<object>(partRow.Table.Columns[XRefColumnAvailable], xRefPN);
                    }
                    else
                    { // Add a new column to the table and populate this row
                        var newXRefColName = "XRef" + (partXRefTableRows.Count() + 1);
                        var newXRefCol = new DataColumn() { ColumnName = newXRefColName };
                        partRow.Table.Columns.Add(newXRefCol);
                        partRow.SetField<object>(newXRefCol, xRefPN);
                    }
                }
            }
            partRow.Table.AcceptChanges();
        }

        private bool IsTableValid(IXLTable table)
        {
            var ret = false;
            ret = table.HeadersRow().Cells().Any(a => a.Value.ToString().ToLower().Contains("part number"));
            if (!ret) return ret;

            ret = table.HeadersRow().Cells().Any(a => a.Value.ToString().ToLower().Contains("xref"));
            return ret;
        }


  
    }
}
