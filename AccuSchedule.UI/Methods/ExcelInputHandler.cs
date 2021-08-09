using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using AccuSchedule.UI.Extensions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text.RegularExpressions;

namespace AccuSchedule.UI.Methods
{
    public class ExcelInputHandler
    {
        public List<IXLTable> Tables { get; set; }
        
        public ExcelInputHandler()
        {
            Tables = new List<IXLTable>();
        }

        public List<IXLTable> ExtractTables(string WorkbookFile, int headerRow = 4, bool writeAccess = false)
        {
            if (string.IsNullOrEmpty(WorkbookFile)) return null;

            var fileAccess = writeAccess ? FileAccess.ReadWrite : FileAccess.Read;

            // Load from FileStream as readonly then input to ClosedXML
            using (var fileStream = new FileStream(WorkbookFile, FileMode.Open, fileAccess, FileShare.ReadWrite))
            {
                using (var xlbook = new XLWorkbook(fileStream, XLEventTracking.Disabled))
                {
                    foreach (var xlsheet in xlbook.Worksheets) // Get each Sheet
                        foreach (var table in xlsheet.Tables) // Get each Table
                            Tables.Add(table);
                }
            }

            return Tables;
            
        }


    }
}
