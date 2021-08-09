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
using ClosedXML.Report;
using System.Diagnostics;
using System.IO;
using System.Collections;
using DocumentFormat.OpenXml.Drawing.Charts;
using DataTable = System.Data.DataTable;
using MoreLinq;
using ClosedXML.Report.Utils;
using Dragablz.Themes;
using DocumentFormat.OpenXml.Spreadsheet;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using System.Media;
using DocumentFormat.OpenXml.Office.Word;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class ExportToTemplateTool : ToolPlugin
    {
        const string title = "Accuplan";

        public override string DefaultSection { get => title; } // Plugin Name (Header used when no type is filtered)
        public override Type[] TypesToLoad => new Type[] { typeof(void) }; // Only load methods with these return types, blank will show all
        public override string NameOfSection(Type methodType) // Name of sections according to return type
        {
            if (methodType is IEnumerable<object>
                || methodType == typeof(void)) return title;

            return string.Empty;
        }

        private static bool PayLoadIsSavable { get; } = false;

        public void ToTemplate(Action<DataSet> Export, IEnumerable<object> objsToOutput, string Template, Button _BrowseForFileTemplate, string SaveTo, Button _BrowseForFileSave)
        {
            if (!File.Exists(Template)) return;


            var engine = BuildTemplateEngine(objsToOutput, Template);
            var results = RunTemplateEngine(engine);

            SystemSounds.Beep.Play(); // Let user know execution is complete

            SaveTemplateResults(engine, SaveTo, false);
            FormatHPageBreaksTemplate(SaveTo, true);

        }

        private string TablePrefix { get; set; }


        #region Export Functionality
        private XLTemplate BuildTemplateEngine(IEnumerable<object> objects, string Template)
        {
            if (objects == null || !objects.Any()) return null;
            if (string.IsNullOrEmpty(Template)) return null;

            // Load the engine with the template
            var engine = new XLTemplate(Template);

            // Add the objects
            foreach (var obj in objects)
            {
                bool addIt = true;
                var dt = obj as DataTable;
                if (dt != null)
                {
                    if (dt.Rows.Count == 0) 
                        addIt = false;
                }

                // Check Dataset for empty tables that could cause errors on template
                var ds = obj as DataSet;
                var tblsToRemove = new List<DataTable>();
                if (ds != null)
                {
                    if (ds.Tables.Count > 0)
                    {
                        foreach (DataTable tbl in ds.Tables)
                        {
                            if (tbl.Rows.Count == 0)
                                tblsToRemove.Add(tbl);
                        }
                    }
                }

                // Remove empty tables from Dataset
                if (tblsToRemove.Count > 0)
                {
                    foreach (var delTbl in tblsToRemove)
                    {
                        // Check for relationship before removing:
                        var relsToRemove = new List<DataRelation>();
                        foreach (DataRelation rels in ds.Relations)
                        {
                            if (rels.ChildTable == delTbl || rels.ParentTable == delTbl)
                                relsToRemove.Add(rels);
                        }

                        // Remove the Relations first
                        foreach (var delRel in relsToRemove)
                        {
                            ds.Relations.Remove(delRel);
                        }

                        // Remove Foreign Key Constraints
                        var constraintsToRemove = new List<ForeignKeyConstraint>();
                        foreach (ForeignKeyConstraint constraint in delTbl.Constraints)
                            constraintsToRemove.Add(constraint);

                        foreach (var delConstraint in constraintsToRemove)
                        {
                            delTbl.Constraints.Remove(delConstraint);
                        }

                        ds.Tables.Remove(delTbl);
                    }
                }
                if (addIt) engine.AddVarToEngineWithBindingSource(obj);
            }


            return engine;
        }

        private XLGenerateResult RunTemplateEngine(XLTemplate engine) => engine.Generate();

        private void SaveTemplateResults(XLTemplate engine, string SaveTo, bool showAfter = true)
        {
            // Save the file
            if (!string.IsNullOrEmpty(SaveTo))
            {
                engine.SaveAs(SaveTo);

                engine.Workbook.Dispose();
                engine.Dispose();

                // Show the file
                if (showAfter)
                    Process.Start(new ProcessStartInfo(SaveTo) { UseShellExecute = true });
            }
        }

        private void FormatHPageBreaksTemplate(string fileName, bool showAfter = true)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            var wb = new XLWorkbook(fileName);

            // Loop through all sheets
            foreach (var workSheet in wb.Worksheets)
            {
                //workSheet.PageSetup.PagesTall = 1;
                workSheet.PageSetup.PagesWide = 1;
                

                var lastCol = workSheet.LastColumnUsed().ColumnLetter();
                var lastRow = workSheet.LastRowUsed().RowNumber();
                workSheet.PageSetup.PrintAreas.Add("A1:" + lastCol + lastRow);

                // Search all cells in col 1
                var formatterFirstColCells = workSheet.Rows()
                        .Where(w => w.Cells("1").FirstOrDefault().Value.ToString().ToLower() == "<<hpagebreak>>")
                        .Select(s => 
                            {
                                s.Cell("1").Value = "";
                                return s.Cell("1").Address.RowNumber - 1; 
                            });

                AddHPageBreak(formatterFirstColCells, workSheet.PageSetup);
            }

            wb.Save();

            if (showAfter)
                Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });

            wb.Dispose();
        }

        private void ExecuteTags(IEnumerable<(string Tag, string Parm, ClosedXML.Excel.IXLCell Cell)> tags, string lastCol, int lastRow)
        {

            var hBreakTags = tags.Where(w => w.Tag.ToLower() == "hpagebreak");
            ExecuteHPageBreakTags(hBreakTags, lastCol, lastRow);

            var nonHBreakTags = tags.Where(w => w.Tag.ToLower() != "hpagebreak");

            for (int i = 0; i < nonHBreakTags.Count(); i++)
            {
                var tag = nonHBreakTags.ElementAt(i);

                switch (tag.Tag.ToLower())
                {
                    case "vpagebreak":
                        AddVPageBreak(tag.Cell, tag.Parm);
                        break;
                    default:
                        break;
                }
            }
        }

        private void ExecuteHPageBreakTags(IEnumerable<(string Tag, string Parm, ClosedXML.Excel.IXLCell Cell)> hBreakTags, string lastCol, int lastRow)
        {
            if (!hBreakTags.Any()) return;

            for (int i = 0; i < hBreakTags.Count(); i++)
            {
                var tag = hBreakTags.ElementAt(i);

                var range = tag.Cell.Address.ColumnLetter + tag.Cell.Address.RowNumber;
                

                if (i == hBreakTags.Count() - 1)
                {
                    range += ":" + lastCol + lastRow;
                }
                else
                {
                    var nextTag = hBreakTags.ElementAt(i + 1);
                    range += ":" + lastCol + (nextTag.Cell.Address.RowNumber - 1);
                }

                //tag.Cell.Worksheet.PageSetup.PrintAreas.Add(range);
                //AddHPageBreak(tag.Cell, tag.Parm);
            }
        }

        private void AddHPageBreak(IEnumerable<int> rows, IXLPageSetup pageSetup)
        {
            foreach (var item in rows)
            {
                pageSetup.AddHorizontalPageBreak(item);
            }
            

        }
        private void AddVPageBreak(ClosedXML.Excel.IXLCell cell, string Parm)
        {
            var pageSetup = cell.Worksheet.PageSetup;
            var Left = false;
            if (Parm.ToLower() == "left") Left = true;


            if (Left)
            {
                if (cell.Address.ColumnNumber > 1)
                    pageSetup.AddVerticalPageBreak(cell.Address.ColumnNumber - 1);
                else
                    pageSetup.AddVerticalPageBreak(cell.Address.ColumnNumber);
            }
            else
            {
                pageSetup.AddVerticalPageBreak(cell.Address.ColumnNumber);
            }
        }
        private IEnumerable<(string Tag, string Parm, ClosedXML.Excel.IXLCell Cell)> ProcessFormatTag(IEnumerable<ClosedXML.Excel.IXLCell> FirstRowCells)
        {
            var ret = new HashSet<(string Tag, string Parm, ClosedXML.Excel.IXLCell Cell)>();


            
            foreach (var rowTags in FirstRowCells)
            {
                var bracksRemoved = rowTags.Value.ToString().Replace("<<", "");
                bracksRemoved = bracksRemoved.Replace(">>", "");
                var splitter = bracksRemoved.Split(' ');

                // If multiple tags are present, seperate by space
                foreach (var splitTag in splitter)
                {
                    var tag = string.Empty;
                    var parm = string.Empty;

                    if (splitTag.Contains("="))
                    {
                        tag = splitTag.Split('=')[0];
                        parm = splitTag.Split('=')[1];
                    }
                    else
                    {
                        tag = splitTag;
                    }

                    ret.Add((tag, parm, rowTags));
                }
            }

            return ret;
        }
        #endregion





    }
}
