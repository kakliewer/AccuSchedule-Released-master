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
using System.Xml.Linq;
using System.IO;
using System.Linq.Dynamic.Core;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.ExtendedProperties;
using AccuSchedule.UI.Views.Dialogs;
using AccuSchedule.UI.ViewModels;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class ReconcileScheduleTool : ToolPlugin
    {
        const string title = "Accuplan";

        public override string DefaultSection { get => title; } // Plugin Name (Header used when no type is filtered)
        public override Type[] TypesToLoad => new Type[] { typeof(DataSet), typeof(System.Data.DataTable) }; // Only load methods with these return types, blank will show all
        public override string NameOfSection(Type methodType) // Name of sections according to return type
        {
            if (methodType is IEnumerable<object>
                || methodType == typeof(System.Data.DataTable)
                || methodType == typeof(System.Data.DataSet)) return title;

            return string.Empty;
        }

        private static bool PayLoadIsSavable { get; } = false;


        public DataSet ReconcileSchedule(DataSet _set, Action<DataSet> Retrieve, string ProcessedPlansLocation, Button _BrowseForDir, string CutFileLocation, Button _BrowseForDir2, string AccuplanLogLocation, Button _BrowseForDir3)
        {
            if (_set == null) return null;


            var TicketsPrefix = string.Empty;
            if (_set.DataSetName.Contains("Schedule"))
                TicketsPrefix = _set.DataSetName.Replace("Schedule", "");

            DataSet ret = null;
            NestingSummary = CreateNestingSummaryTable(TicketsPrefix);
            

            
            if (!string.IsNullOrEmpty(ProcessedPlansLocation))
            {
                WatchDirectory = ProcessedPlansLocation;
                var tickets = ProcessTickets(_set);

                ret = ConvertToDataSet(tickets, TicketsPrefix, CutFileLocation, _set);
            }

            // Create "MarkerOverview" sheet that's populated by Batch and Que logs... batch first as it has the ID#
            PopulateMarkersOverviewFromLogs(AccuplanLogLocation, _set);
            

            return ret;
        }

        private DataSet ogSet { get; set; }

        private System.Data.DataTable NestingSummary { get; set; }

        private void PopulateMarkersOverviewFromLogs(string logLocation, DataSet travelersSet)
        {
            if (string.IsNullOrEmpty(logLocation)) return;

            NestingSummary.Rows.Clear();

            var linesInLog = GetLinesFromBatchFile(logLocation);

            // Search for Marker Name and pull the previous 2 items.
            foreach (var markerRow in markersTable.AsEnumerable())
            {
                var markerName = markerRow.Field<object>("Name").ToString();
                var matchingRows = linesInLog.Where(w => w.Contains(markerName));
                var travelerID = markerRow.Field<object>("TravelerID").ToString();


                var rowsProcessed = new List<string>();
                foreach (var matchedRow in matchingRows)
                {
                    var matchedRowIndex = linesInLog.IndexOf(matchedRow);
                    var timeRow = string.Empty;
                    if (matchedRowIndex > 0)
                    {
                        timeRow = linesInLog.ElementAt(matchedRowIndex - 2);
                        rowsProcessed.Add(linesInLog.ElementAt(matchedRowIndex - 2)); // Date line
                        rowsProcessed.Add(linesInLog.ElementAt(matchedRowIndex - 1)); // Headers
                        rowsProcessed.Add(matchedRow); // Info
                    }

                    ProcessBatchLines(timeRow, matchedRow, travelerID);
                    
                }
                // Remove the rows from the list
                foreach (var rowToRemove in rowsProcessed)
                    linesInLog.Remove(rowToRemove);
                
            }

            // Rewrite the batch file with remaining lines
            var remainingBatch = string.Empty;
            foreach (var remainingLine in linesInLog)
                remainingBatch += remainingLine + Environment.NewLine;

            //! Undo when ready
            //File.WriteAllText(logLocation + "\\BatchQueue.log", remainingBatch);


            // Process the UltraQueue
            ProcessUltraQueue(logLocation);
        }

        private void ProcessUltraQueue(string logLocation)
        {
            // Get all Folders.. if nesting is started a directory will be created matching the job num
            var umqFile = logLocation + "\\ultramrk.umq";
            var lines = ExtractQueInfo(umqFile);

            foreach (var batchEntry in NestingSummary.AsEnumerable())
            {
                // Check for a matching directory, if found- populate LastExecuted and Eff, otherwise populate LastExecuted "Nesting Not Started."
                var jobNum = batchEntry.Field<object>("JobNum").ToString();
                var markerName = batchEntry.Field<object>("Marker").ToString();
                var travelerID = batchEntry.Field<object>("TravelerID").ToString();

                ProcessQueInfo(lines, batchEntry, markerName);

            }
        }
        private void ProcessQueInfo(List<string> newLines, DataRow batchRow, string MarkerName)
        {
            var markerLineAndID = newLines.Where(w => w.StartsWith(MarkerName.ToLower())).Select((s, Id) => new { line = s, idx = Id }).FirstOrDefault();

            if (markerLineAndID == null)
            {
                markerLineAndID = newLines.Where(w => w.StartsWith("finishtime") && w.Contains(MarkerName.ToLower())).Select((s, Id) => new { line = s, idx = Id }).FirstOrDefault();
            }

            if (markerLineAndID != null)
            {
                var markerLine = markerLineAndID.line;
                var lineIndex = newLines.IndexOf(markerLine, markerLineAndID.idx > 0 ? markerLineAndID.idx - 1 : markerLineAndID.idx);

                var submitTimeFound = false;
                var searchIdx = lineIndex - 1;

                var typeOfAction = "Nesting Incomplete";

                // Find the start line
                do
                {
                    var prevLine = newLines.ElementAt(searchIdx);
                    if (prevLine.StartsWith("submittime"))
                    {
                        lineIndex = searchIdx;
                        submitTimeFound = true;

                        // Get the type of action
                        if (prevLine.Contains("typecommit"))
                            typeOfAction = "Success";
                        else if (prevLine.Contains("typefraun"))
                            typeOfAction = "Nesting...";

                    }
                    else
                    {
                        searchIdx -= 1;
                    }
                    if (searchIdx < 0)
                    {
                        lineIndex = 0;
                        submitTimeFound = true;
                    }
                } while (!submitTimeFound);


                // Find the end line
                searchIdx = lineIndex + 1;
                submitTimeFound = false;
                var endIdx = searchIdx;
                do
                {
                    var nextLine = newLines.ElementAt(searchIdx);
                    if (nextLine.StartsWith("submittime"))
                    {
                        endIdx = searchIdx - 1;
                        submitTimeFound = true;
                    }
                    else
                    {
                        searchIdx += 1;
                    }
                    if (searchIdx >= newLines.Count - 1)
                    {
                        endIdx = newLines.Count;
                        submitTimeFound = true;
                    }
                } while (!submitTimeFound);

                for (int i = lineIndex; i < endIdx; i++)
                {
                    // Process each section that relates to the marker
                    var sectionStart = newLines.ElementAt(i);

                    var effLine = newLines.ElementAt(i);
                    if (effLine.StartsWith("efficiency"))
                    {
                        var effAIndex = effLine.IndexOf("\a") + 1;
                        var eff = string.Empty;
                        if (effAIndex >= 0)
                        {
                            eff = effLine.Substring(effAIndex, effLine.Length - effAIndex);
                            eff = eff.Substring(0, eff.IndexOf("\u0014"));
                            decimal effDec = 0;
                            bool canConvert = decimal.TryParse(eff, out effDec);
                            batchRow.SetField("MarkerEff", canConvert ? effDec : 0);
                            batchRow.SetField("NestingStatus", canConvert ? "Success" : typeOfAction);
                        }
                    }
                }


            }
            else
            {
                batchRow.SetField("MarkerEff", 0);
                batchRow.SetField("NestingStatus", "Nesting Incomplete.");
            }
        }
        private List<string> ExtractQueInfo(string umqFile)
        {

            // Read the file and display it line by line.;
            List<string> newLines = new List<string>();
            using (FileStream fs = File.Open(umqFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sr = new StreamReader(fs))
                {
                    var line = string.Empty;
                    while ((line = sr.ReadLine()) != null)
                    {
                        newLines.Add(line);
                    }
                    sr.Close();
                }
                fs.Close();
            }

            return newLines;
        }
        private List<string> GetLinesFromBatchFile(string logLocation)
        {
            var batchFile = logLocation + "\\BatchQueue.log";

            // Get All Lines from batch log
            string line = string.Empty;
            List<string> lines = new List<string>();
            if (File.Exists(batchFile))
            {
                // Read the file and display it line by line.
                var fs = new FileStream(batchFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (var sr = new StreamReader(fs))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                    sr.Close();
                }
            }
            return lines;
        }

        private System.Data.DataTable CreateNestingSummaryTable(string TicketsPrefix)
        {
            var ret = new System.Data.DataTable() { TableName = TicketsPrefix + "CtNestingStatus" };

            // Add the columns
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "JobNum" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Marker" });
            ret.Columns.Add(new DataColumn() { ColumnName = "BatchStatus" });
            ret.Columns.Add(new DataColumn() { ColumnName = "StartTime" });
            ret.Columns.Add(new DataColumn() { ColumnName = "FinishTime" });

            ret.Columns.Add(new DataColumn() { ColumnName = "NestingStatus" });
            ret.Columns.Add(new DataColumn() { ColumnName = "MarkerEff" });
            return ret;
        }
        private void ProcessBatchLines(string DateLine, string InfoLine, string travelerID)
        {
            var dateExtract = string.Empty;
            if (DateLine.Length > 18)
                dateExtract = DateLine.Substring(18, DateLine.Length - 18);

            var delim = "\t";
            var infoSplit = InfoLine.Split(delim.ToCharArray());
            var newRow = NestingSummary.NewRow();
            newRow.SetField("TravelerID", travelerID);
            newRow.SetField("StartTime", dateExtract);

            if (infoSplit.Length == 12)
            {
                newRow.SetField("JobNum", Math.Truncate(Convert.ToDecimal(infoSplit[0])));
                newRow.SetField("Marker", infoSplit[2]);
                newRow.SetField("BatchStatus", infoSplit[10]);
                newRow.SetField("FinishTime", infoSplit[11]);
            } else
            {
                newRow.SetField("JobNum", Math.Truncate(Convert.ToDecimal(infoSplit[0])));
                newRow.SetField("Marker", infoSplit[4]);
                newRow.SetField("BatchStatus", infoSplit[12]);
                newRow.SetField("FinishTime", infoSplit[13]);
            }

            NestingSummary.Rows.Add(newRow);
            //for (int i = 0; i < infoSplit.Length; i++)
            //{
            //   switch (i)
            //    {
            //        case 0: // Job #
            //            newRow.SetField("JobNum", Math.Truncate(Convert.ToDecimal(infoSplit[i])));
            //            break;
            //        case 1: // Batch Table Location
            //            break;
            //        case 2: // Marker Name
            //            newRow.SetField("Marker", infoSplit[i]);
            //            break;
            //        case 3: // Marker Name (Duplicate)
            //            break;
            //        case 4: // Method
            //            break;
            //        case 5: // Plot?
            //           break;
            //        case 6: // Cut?
            //            break;
            //        case 7: // Priority
            //            break;
            //        case 8: // Cut File Name
            //            break;
            //        case 9: // Group (Blank)
            //            break;
            //        case 10: // Status ("FAIL", "COMPLETED")
            //           newRow.SetField("BatchStatus", infoSplit[i]);
            //            break;
            //        case 11: // Completed Time
            //            newRow.SetField("FinishTime", infoSplit[i]);
            //            break;
            //        default:
            //            break;
            //    }
            //}

        }


        private bool GetAll { get; set; } = false; // If True all files will be retrieved from the directory regardless if their on the schedule or not.
        private string WatchDirectory { get; set; }
        private string FileNameExtensions { get; set; } = "*.xml";
        private List<CutTicket> CutTickets { get; set; }

        private string[] _Files { get; set; }

        private System.Data.DataTable cutTicketsTable { get; set; }
        private System.Data.DataTable modelsTable { get; set; }
        private System.Data.DataTable markersTable { get; set; }
        private System.Data.DataTable markerModelsTable { get; set; }
        private System.Data.DataTable markerMaterialsTable { get; set; }


        public List<CutTicket> ProcessTickets(DataSet _set)
        {
            CutTickets = new List<CutTicket>();

            ogSet = _set;

            // Get the traveler IDS
            System.Data.DataTable travelerTable = null;
            var travelerIDs = new HashSet<string>();
            foreach (System.Data.DataTable table in _set.Tables)
                if (table.TableName.Contains("Travelers"))
                {
                    travelerTable = table;
                    foreach (var row in table.AsEnumerable())
                        travelerIDs.Add(row.Field<object>("TravelerID")?.ToString());
                }

            if (GetAll)
            {
                CutTickets.AddRange(GetAllTickets(GetFiles(), 
                ((string fileName, string orderID) fileInfo) => ProcessXML(fileInfo.fileName, fileInfo.orderID, false)));
            } else
            {
                CutTickets.AddRange(GetTickets(travelerIDs, GetFiles(), 
                ((string fileName, string orderID) fileInfo) => ProcessXML(fileInfo.fileName, fileInfo.orderID, false)));
            }
            

            return CutTickets;
        }
        List<CutTicket> ProcessXML(string filePath, string cutTicketID, bool deleteWhenFinished = false)
        {
            // Open the file, read the XML
            XDocument _doc = XDocument.Load(filePath);

            List<CutTicket> retList = new List<CutTicket>();

            DateTime dueDate = DateTime.Now;
            _ = DateTime.TryParse(_doc.Root.Element("Information").Element("CutDueDate").Value, out dueDate);

            IEnumerable<XElement> _models = _doc.Root.Element("Models").Elements("Model"); // Get the input Models being planned
            var inputModels = ProcessInputModels(_models); // Store the input Models

            // Loop through cut plans and get each result
            IEnumerable<XElement> _cutPlans = _doc.Root.Elements("CutPlans");
            foreach (XElement _cutPlan in _cutPlans.Elements())
            {
                // Create a new Cut Ticket
                var plan = ProcessPlan(_cutPlan, inputModels, cutTicketID, dueDate);
                retList.Add(plan);
            }

            if (deleteWhenFinished) if (File.Exists(filePath)) File.Delete(filePath); // Delete XML File

            if (!retList.Any()) return null;

            return retList;
        }
        List<ctInputModels> ProcessInputModels(IEnumerable<XElement> models)
        {
            List<ctInputModels> retList = new List<ctInputModels>();

            if (models.Any())
            {
                // Loop through each Model and add to list
                foreach (var model in models)
                {
                    ctInputModels inputModel = new ctInputModels();
                    inputModel.Name = model.Attribute("Name").Value;

                    IEnumerable<XElement> fabricCodes = model.Element("FabricCodes").Elements("Code");
                    string SizeLine = model.Element("SizeLine").Attribute("FabricCodes").Value;
                    IEnumerable<XElement> sizeIDs = model.Element("SizeLine").Elements("Size");

                    // If there are fabric codes then add them to the inputModel
                    if (fabricCodes.Any())
                    {
                        inputModel.FabricCodes = new List<ctFabricCodes>();
                        foreach (var fabCode in fabricCodes)
                        {
                            ctFabricCodes fabricCode = new ctFabricCodes();
                            fabricCode.FabricCode = fabCode.Value;
                            fabricCode.SizeLine = SizeLine;
                            fabricCode.SizeID = sizeIDs.Where(w => w.Value == fabCode.Value).Select(s => s.Attribute("Id").Value).ToList();

                            inputModel.FabricCodes.Add(fabricCode); // Add fabric codes to Model
                        }
                    }

                    retList.Add(inputModel); // Add model to return list
                }

            }
            else
            {
                retList = null;
            }

            return retList;

        }
        CutTicket ProcessPlan(XElement cutPlan, List<ctInputModels> inputModels, string cutTicketID, DateTime dateDue)
        {
            CutTicket ct = new CutTicket();
            ct.Input_Models = inputModels;
            ct.ID = cutTicketID;
            ct.FabricCodes = cutPlan.Element("FabricCodes")?.Elements("Code")?.Select(s => s.Value).ToList();
            ct.DateRequested = dateDue;

            List<ctModels> modelList = new List<ctModels>();

            // Create new model retList
            foreach (var model in inputModels)
            {
                ctModels newModel = new ctModels();
                newModel.Name = model.Name;
                newModel.SizeID = model.FabricCodes.First().SizeID.First();
                newModel.Material = new List<ctModelMat>();

                modelList.Add(newModel);
            }

            // #### Process the Inputs
            // Process the BundleMeasures
            XElement _input = cutPlan.Element("Input");
            IEnumerable<XElement> _bundleMeasurements = _input.Element("BundleMeasures")?.Elements("Bundle");
            foreach (var bundle in _bundleMeasurements)
            {
                int pcQty = Convert.ToInt32(bundle.Attribute("PieceQuantity").Value);
                string sizeId = bundle.Attribute("SizeId").Value;
                double perim = double.Parse(bundle.Attribute("Perimeter").Value);

                // Find the size id in the retList [SEARCH]
                var sizeInRetList = modelList.Where(w => w.SizeID == sizeId).First();
                sizeInRetList.PcQty = pcQty;
                sizeInRetList.Perimeter = perim;
            }

            // Process the BundleInfo
            IEnumerable<XElement> _bundleInfo = _input.Element("BundleInfo")?.Elements("Bundle");
            foreach (var bundle in _bundleInfo)
            {
                string sizeId = bundle.Attribute("SizeId").Value;
                int outputQty = Convert.ToInt32(bundle.Attribute("OutputQuantity").Value);

                var sizeInRetList = modelList.Where(w => w.SizeID == sizeId).First();
                sizeInRetList.Qty = outputQty;
            }

            // Process the Colors/Material for each bundle
            IEnumerable<XElement> colors = _input.Elements("Color");
            foreach (var color in colors)
            {
                string matName = color.Attribute("Name").Value;
                IEnumerable<XElement> _bundleColor = color.Elements("Bundle");
                foreach (var bundle in _bundleColor)
                {

                    string sizeId = bundle.Attribute("SizeId").Value;
                    int qty = Convert.ToInt32(bundle.Attribute("Quantity").Value);

                    ctModelMat modelMat = new ctModelMat();
                    modelMat.Name = matName;
                    modelMat.Qty = qty;

                    if (modelList.Where(w => w.SizeID == sizeId).First().Qty == 0)
                    {
                        var modelID = modelList.Where(w => w.SizeID == sizeId).First();
                        modelID.Qty = qty;
                    }

                    var sizeInRetList = modelList.Where(w => w.SizeID == sizeId).First();
                    sizeInRetList.Material.Add(modelMat);
                }
            }

            ct.Models = modelList;



            // #### Process the markers
            List<ctMarkers> markerList = new List<ctMarkers>();

            // Process the results
            XElement _result = cutPlan.Element("Result");

            if (string.IsNullOrEmpty(_result.Attribute("FabricRequired")?.Value))
            { // Error Missing Components
                //Log.Error("Missing components for Model {0}", ct.ID);
                ct.FabricRequired = 0;
            }
            else
            {
                ct.FabricRequired = double.Parse(_result.Attribute("FabricRequired")?.Value);
            }



            IEnumerable<XElement> _MarkerSpreadings = _result.Element("MarkerSpreadings").Elements("MarkerSpreading");
            foreach (var _marker in _MarkerSpreadings)
            {

                ctMarkers newMarker = new ctMarkers();
                
                double planned = 0;
                if (_marker.Attributes().Any(a => a.Name == "FabricRequired"))
                {
                    bool res = double.TryParse(_marker.Attribute("FabricRequired").Value, out planned);
                }
                newMarker.PlannedFabricIN = planned;
                newMarker.Name = _marker.Element("Marker").Attribute("Name").Value;
                newMarker.MatWidth = double.Parse(_marker.Element("Marker").Attribute("Width").Value);
                newMarker.TargetUtilization = Math.Round(double.Parse(_marker.Element("Marker").Attribute("Utilization").Value), 2);

                // Process the Marker Models
                IEnumerable<XElement> _sectionBundles = _marker.Element("Marker").Element("Section").Elements("Bundle");
                newMarker.Models = new List<ctMarkerModels>();
                var mList = new List<ctMarkerModels>();
                foreach (var bundle in _sectionBundles)
                {
                    string sizeId = bundle.Attribute("SizeId").Value;
                    string qty = bundle.Attribute("Quantity").Value;

                    var sizeInRetList = modelList.Where(w => w.SizeID == sizeId).First();
                    ctMarkerModels newModel = new ctMarkerModels();
                    newModel.Name = sizeInRetList.Name;
                    newModel.Qty = Convert.ToInt32(qty);

                    mList.Add(newModel);
                }


                // Process the Marker Materials
                IEnumerable<XElement> _spreadSets = _marker.Element("SpreadSets").Elements("SpreadSet");
                newMarker.Material = new List<ctMarkerMaterial>();
                foreach (var spreadSet in _spreadSets)
                {
                    ctMarkerMaterial newMat = new ctMarkerMaterial();
                    newMat.Name = spreadSet.Element("plySet").Attribute("Color").Value;
                    newMat.SpreadQty = Convert.ToInt32(spreadSet.Attribute("SpreadQuantity").Value);
                    newMat.PlyQty = Convert.ToInt32(spreadSet.Element("plySet").Attribute("PlyQuantity").Value);

                    var fabreq = spreadSet.Attribute("FabricRequired");
                    if (ct.FabricRequired > 0 && !string.IsNullOrEmpty(fabreq?.Value))
                        newMat.PlyLength = double.Parse(spreadSet.Attribute("FabricRequired").Value) / newMat.PlyQty;

                    newMat.TotalPlys = newMat.PlyQty * newMat.SpreadQty;
                    newMat.SpreadSet = Convert.ToInt32(spreadSet.Attribute("Id").Value);

                    newMarker.Material.Add(newMat);

                    // Add models per spreadset
                    foreach (var modelFromMarker in mList)
                    {
                        var modelQty = modelFromMarker.Qty * newMat.TotalPlys;
                        var newMod = new ctMarkerModels()
                        {
                            Name = modelFromMarker.Name,
                            Qty = modelFromMarker.Qty,
                            QtyTotal = modelQty,
                            SpreadSet = newMat.SpreadSet
                        };
                        newMarker.Models.Add(newMod);
                    }
                }

                markerList.Add(newMarker);

                }

            ct.Markers = markerList;

            return ct;
        }

        private static IEnumerable<CutTicket> GetTickets(IEnumerable<string> travelerIDs, string[] files, Func<(string fileName, string orderID), List<CutTicket>> ProcessXMLFunction)
        {
            var ret = new List<CutTicket>();

            if (travelerIDs == null || !travelerIDs.Any()) return ret;

            foreach (var travelerID in travelerIDs)
            {
                var anyFiles = files.Where(w => w.Contains(travelerID)); // Search for file
                if (anyFiles.Any())
                {
                    var methArgs = (anyFiles.First(), travelerID);
                    ret.AddRange(ProcessXMLFunction.Invoke(methArgs));
                }
            }

            if (!ret.Any())
                MessageBox.Show("No files found matching Traveler ID's.", "Not Found!", MessageBoxButton.OK, MessageBoxImage.Warning);


            return ret;
        }
        private static IEnumerable<CutTicket> GetAllTickets(string[] files, Func<(string fileName, string orderID), List<CutTicket>> ProcessXMLFunction)
        {
            var ret = new List<CutTicket>();


            if (files.Any())
            {
                foreach (var file in files)
                {
                    var methArgs = (file, Path.GetFileName(file.ToLower().Contains(".xml") ? file.Substring(0, length:(file.Length) - 4) : file ));
                    ret.AddRange(ProcessXMLFunction.Invoke(methArgs));
                }
                
            }
            

            return ret;
        }
        public string[] GetFiles(bool CheckDirAgain = false) =>
            CheckDirAgain
                ? _Files = Directory.GetFiles(WatchDirectory, FileNameExtensions)
                : _Files != null && _Files.Any()
                    ? _Files
                    : _Files = Directory.GetFiles(WatchDirectory, FileNameExtensions);


        private DataSet ConvertToDataSet(List<CutTicket> tickets, string TicketsPrefix, string CutFileLocation, DataSet _set)
        {
            var res = new DataSet();
            res.DataSetName = TicketsPrefix + "CutTickets"; //? Later change this to using the attached dataset

            cutTicketsTable = CreateCutTicketsTable(TicketsPrefix);
            modelsTable = CreateModelsTable(TicketsPrefix);
            markersTable = CreateMarkersTable(TicketsPrefix);
            markerModelsTable = CreateMarkerModelsTable(TicketsPrefix);
            markerMaterialsTable = CreateMarkerMaterialsTable(TicketsPrefix);

            foreach (var ticket in tickets)
            {
                if (!cutTicketsTable.AsEnumerable().Any(w => w.Field<object>("TravelerID").ToString() == ticket.ID))
                {
                    // Add the ticket Info
                    var ctRow = cutTicketsTable.NewRow();
                    ctRow.SetField("TravelerID", ticket.ID);
                    ctRow.SetField("FabricRequired", ticket.FabricRequired);
                    cutTicketsTable.Rows.Add(ctRow);

                    
                    foreach (var model in ticket.Models)
                    {
                        

                        // Add the model materials info
                        foreach (var material in model.Material)
                        {
                            // Each model material row requires it's own model row
                            var mRow = modelsTable.NewRow();
                            mRow.SetField("TravelerID", ticket.ID);
                            mRow.SetField("Name", model.Name);
                            mRow.SetField("PcQty", model.PcQty);
                            mRow.SetField("Qty", model.Qty);
                            mRow.SetField("Perimeter", model.Perimeter);
                            mRow.SetField("Material", material.Name);
                            mRow.SetField("MaterialAmount", material.Qty);
                            
                            modelsTable.Rows.Add(mRow);
                        }
                    }

                    // Add the markers info
                    var allMarkers = markersTable.Clone();
                    var allModels = markerModelsTable.Clone();
                    var allMaterials = markerMaterialsTable.Clone();
                    foreach (var marker in ticket.Markers)
                    {
                        var mrkRow = allMarkers.NewRow();
                        mrkRow.SetField("TravelerID", ticket.ID);
                        mrkRow.SetField("Name", marker.Name);
                        mrkRow.SetField("PlannedWidth", marker.MatWidth);
                        mrkRow.SetField("TotalPlannedFabricIN", marker.PlannedFabricIN);
                        mrkRow.SetField("TotalActualFabricIN", 0);
                        mrkRow.SetField("ActualWidth", 0);
                        mrkRow.SetField("PlannedPlyLengthIN", Math.Round(marker.PlannedFabricIN / marker.Material.Sum(add => add.TotalPlys), 3));
                        mrkRow.SetField("ActualPlyLengthIN", 0);
                        mrkRow.SetField("TargetUtilization", marker.TargetUtilization);
                        allMarkers.Rows.Add(mrkRow);

                        // Add the marker models info
                        foreach (var model in marker.Models)
                        {
                            var mdl = allModels.NewRow();
                            mdl.SetField("TravelerID", ticket.ID);
                            mdl.SetField("MarkerName", marker.Name);
                            mdl.SetField("Name", model.Name);
                            mdl.SetField("SpreadSet", model.SpreadSet);
                            mdl.SetField("Qty", model.Qty);
                            mdl.SetField("QtyTotal", model.QtyTotal);
                            allModels.Rows.Add(mdl);
                           
                        }

                        // Add the marker materials info
                        foreach (var material in marker.Material)
                        {
                            var mtl = allMaterials.NewRow();
                            mtl.SetField("TravelerID", ticket.ID);
                            mtl.SetField("MarkerName", marker.Name);
                            mtl.SetField("Name", material.Name);
                            mtl.SetField("SpreadQty", material.SpreadQty);
                            mtl.SetField("PlyQty", material.PlyQty);
                            mtl.SetField("TotalPlys", material.TotalPlys);
                            mtl.SetField("SpreadSet", material.SpreadSet);

                            allMaterials.Rows.Add(mtl);
                        }
                    }

                    // Popoulate from the groupings
                    PopulateMarkersBasedOnGrouping(allMarkers);
                    PopulateMarkerModelsBasedOnGrouping(allModels, _set, TicketsPrefix);
                    PopulateMarkerMaterialsBasedOnGrouping(allMaterials);

                }

            }

            res.Tables.Add(cutTicketsTable);
            res.Tables.Add(modelsTable);
            res.Tables.Add(markersTable);
            res.Tables.Add(markerModelsTable);
            res.Tables.Add(markerMaterialsTable);
            res.Tables.Add(NestingSummary);
            AddRelationships(res);

            // Populate the acutals
            PopulateActuals(markersTable.AsEnumerable().Where(w => !string.IsNullOrEmpty(w.Field<object>("Name").ToString())), CutFileLocation);

            return res;
        }

        private HashSet<string> PopulateActuals(IEnumerable<DataRow> markerRows, string CutFileLocation)
        {
            var markersNotFound = new HashSet<string>();

            foreach (var markerRow in markerRows)
            {
                var markerName = markerRow.Field<object>("Name").ToString();
                string cutFileFullPath = CutFileLocation + "\\" + markerName + ".CUT";

                if (File.Exists(cutFileFullPath))
                { // Found file, now read it.
                    string fileText = ReadCharsFromFile(cutFileFullPath, 100);

                    int lengthStart = fileText.IndexOf("/L=", StringComparison.Ordinal) + 3;
                    int lengthEnd = fileText.IndexOf("IN/", lengthStart, StringComparison.Ordinal);
                    int widthStart = fileText.IndexOf("W=", lengthEnd, StringComparison.Ordinal) + 2;
                    int widthEnd = fileText.IndexOf("IN", widthStart, StringComparison.Ordinal);
                    double markerLengthIN = double.Parse(fileText.Substring(lengthStart, lengthEnd - lengthStart));
                    double markerWidthIN = double.Parse(fileText.Substring(widthStart, widthEnd - widthStart));

                    // Get the marker details
                    var markerMaterials = markerRow.GetChildRows("MarkerMaterials");
                    var totalPlys = markerMaterials.Sum(s => Convert.ToInt32(s.Field<object>("PlyQty").ToString()) * Convert.ToInt32(s.Field<object>("SpreadQty").ToString()));

                    if (markerLengthIN > 0)
                    {
                        markerRow.SetField("TotalActualFabricIN", Math.Round(markerLengthIN * totalPlys, 3));
                        markerRow.SetField("ActualPlyLengthIN", Math.Round(markerLengthIN, 3));
                    }

                    if (markerWidthIN > 0)
                    {
                        markerRow.SetField("ActualWidth", markerWidthIN);
                    }


                    




                }
            }

            

            return markersNotFound;
        }
        private static string ReadCharsFromFile(string filename, int count)
        {
            using (var stream = File.OpenRead(filename))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                char[] buffer = new char[count];
                int n = reader.ReadBlock(buffer, 0, count);

                char[] result = new char[n];

                Array.Copy(buffer, result, n);

                return new string(result);
            }
        }






        private void PopulateMarkerMaterialsBasedOnGrouping(System.Data.DataTable allMaterials)
        {


            // Group the Markers and repopulate.
            var groupedMaterials = allMaterials.AsEnumerable()
                .GroupBy(g => new
                {
                    TravelerID = g.Field<object>("TravelerID").ToString(),
                    MarkerName = g.Field<object>("MarkerName").ToString(),
                    Name = g.Field<object>("Name").ToString(),
                    SpreadQty = Convert.ToInt32(g.Field<object>("SpreadQty").ToString()),
                    PlyQty = Convert.ToInt32(g.Field<object>("PlyQty").ToString()),
                    SpreadSet = Convert.ToInt32(g.Field<object>("SpreadSet").ToString()),
                    TotalPlys = Convert.ToInt32(g.Field<object>("TotalPlys").ToString())
                })
                .Select(s => new
                {
                    TravelerID = s.Key.TravelerID,
                    MarkerName = s.Key.MarkerName,
                    SpreadSet = s.Key.SpreadSet,
                    Name = s.Key.Name,
                    SpreadQty = s.Key.SpreadQty,
                    PlyQty = s.Key.PlyQty,
                    TotalPlys = s.Key.TotalPlys
                });

            // Add the rows
            foreach (var model in groupedMaterials)
            {
                var mrkRow = markerMaterialsTable.NewRow();
                mrkRow.SetField("TravelerID", model.TravelerID);
                mrkRow.SetField("MarkerName", model.MarkerName);
                mrkRow.SetField("SpreadSet", model.SpreadSet);
                mrkRow.SetField("Name", model.Name);
                mrkRow.SetField("SpreadQty", model.SpreadQty);
                mrkRow.SetField("PlyQty", model.PlyQty);
                mrkRow.SetField("TotalPlys", model.TotalPlys);


                // Get material PN from the name then search it in the "Materials" table from the _set
                var matPN = string.Empty;
                if (model.Name.Contains(","))
                {
                    matPN = model.Name.Substring(model.Name.IndexOf(',') + 1).Trim();
                }
                else
                {
                    matPN = model.Name;
                }
                mrkRow.SetField("MaterialPN", matPN);


                // Find the material in the _"schedule set" 
                var matDesc = string.Empty;
                foreach (System.Data.DataTable table in ogSet.Tables)
                {
                    if (table.TableName.EndsWith("Materials"))
                    {
                        matDesc = table.AsEnumerable().Where(w => w.Field<object>("MaterialPN")?.ToString().Trim() == matPN).FirstOrDefault()?.Field<object>("Desc1")?.ToString();
                        break;
                    }
                }
                mrkRow.SetField("MaterialDesc", matDesc);

                markerMaterialsTable.Rows.Add(mrkRow);
            }


        }
        private void PopulateMarkerModelsBasedOnGrouping(System.Data.DataTable allModels, System.Data.DataSet _set, string TicketsPrefix)
        {
            // Group the Markers and repopulate.
            var groupedModel = allModels.AsEnumerable()
                .GroupBy(g => new
                {
                    TravelerID = g.Field<object>("TravelerID").ToString(),
                    MarkerName = g.Field<object>("MarkerName").ToString(),
                    Name = g.Field<object>("Name").ToString(),
                    SpreadSet = g.Field<object>("SpreadSet").ToString(),
                })
                .Select(s => new
                {
                    TravelerID = s.Key.TravelerID,
                    MarkerName = s.Key.MarkerName,
                    Name = s.Key.Name,
                    Qty = s.Sum(add => Convert.ToInt32(add.Field<object>("Qty").ToString())),
                    QtyTotal = s.Sum(add => Convert.ToInt32(add.Field<object>("QtyTotal").ToString())),
                    SpreadSet = s.Key.SpreadSet
                });

            // Add the rows
            // Add the models info
            var lstNames = new HashSet<string>();
            var lstDesc = new HashSet<string>();
            var lstDesc2 = new HashSet<string>();
            var parentNames = string.Empty;
            var parentDescs = string.Empty;
            var parentDescs2 = string.Empty;
            foreach (var model in groupedModel)
            {
                // Get the Parent and Description for the
                foreach (System.Data.DataTable tbl in _set.Tables)
                {
                    if (tbl.TableName == TicketsPrefix + "Parts")
                    {
                        var pnRecords = tbl.AsEnumerable()
                            .Where(w => w.Field<object>("TravelerID").ToString() == model.TravelerID);

                        foreach (var pnRow in pnRecords)
                        {
                            var pnField = pnRow.Field<object>("xRefParent").ToString();
                            lstNames.Add(pnField);

                            var descField = pnRow.Field<object>("Desc1").ToString();
                            lstDesc.Add(descField);

                            var desc2Field = pnRow.Field<object>("Desc2").ToString();
                            lstDesc2.Add(desc2Field);
                        }

                        parentNames = string.Join(" | ", lstNames);
                        parentDescs = string.Join(" | ", lstDesc);
                        parentDescs2 = string.Join(" | ", lstDesc2);
                    }
                }

                var mrkRow = markerModelsTable.NewRow();
                mrkRow.SetField("TravelerID", model.TravelerID);
                mrkRow.SetField("MarkerName", model.MarkerName);
                mrkRow.SetField("SpreadSet", model.SpreadSet);
                mrkRow.SetField("Name", model.Name);
                mrkRow.SetField("Parent", parentNames);
                mrkRow.SetField("Desc1", parentDescs);
                mrkRow.SetField("Desc2", parentDescs2);
                mrkRow.SetField("Qty", model.Qty);
                mrkRow.SetField("QtyTotal", model.QtyTotal);
                markerModelsTable.Rows.Add(mrkRow);
            }


        }
        private void PopulateMarkersBasedOnGrouping(System.Data.DataTable allMarkers)
        {
            // Group the Markers and repopulate.
            var groupedMarkers = allMarkers.AsEnumerable()
                .GroupBy(g => new
                {
                    TravelerID = g.Field<object>("TravelerID").ToString(),
                    Name = g.Field<object>("Name").ToString()
                })
                .Select(s => new
                {
                    TravelerID = s.Key.TravelerID,
                    Name = s.Key.Name,
                    PlannedWidth = s.Sum(add => Convert.ToDecimal(add.Field<object>("PlannedWidth").ToString())),
                    ActualWidth = s.Sum(add => Convert.ToDecimal(add.Field<object>("ActualWidth").ToString())),
                    TotalPlannedFabricIN = s.Sum(add => Convert.ToDecimal(add.Field<object>("TotalPlannedFabricIN").ToString())),
                    TotalActualFabricIN = s.Sum(add => Convert.ToDecimal(add.Field<object>("TotalActualFabricIN").ToString())),
                    PlannedPlyLengthIN = s.Sum(add => Convert.ToDecimal(add.Field<object>("PlannedPlyLengthIN").ToString())),
                    ActualPlyLengthIN = s.Sum(add => Convert.ToDecimal(add.Field<object>("ActualPlyLengthIN").ToString())),
                    TargetUtilization = s.Average(avg => Convert.ToDecimal(avg.Field<object>("TargetUtilization").ToString()))
                });

            // Add the rows
            foreach (var marker in groupedMarkers)
            {
                var mrkRow = markersTable.NewRow();
                mrkRow.SetField("TravelerID", marker.TravelerID);
                mrkRow.SetField("Name", marker.Name);
                mrkRow.SetField("PlannedWidth", marker.PlannedWidth);
                mrkRow.SetField("TotalPlannedFabricIN", marker.TotalPlannedFabricIN);
                mrkRow.SetField("TotalActualFabricIN", marker.TotalActualFabricIN);
                mrkRow.SetField("ActualWidth", marker.ActualWidth);
                mrkRow.SetField("PlannedPlyLengthIN", marker.PlannedPlyLengthIN);
                mrkRow.SetField("ActualPlyLengthIN", marker.ActualPlyLengthIN);
                mrkRow.SetField("TargetUtilization", marker.TargetUtilization);
                markersTable.Rows.Add(mrkRow);

            }
            

        }


        private void AddRelationships(DataSet set)
        {
            // Cut Ticket Relations
            set.Relations.Add("Models",
                new DataColumn[] { cutTicketsTable.Columns["TravelerID"] },
                new DataColumn[] { modelsTable.Columns["TravelerID"] });
            set.Relations.Add("Markers",
                new DataColumn[] { cutTicketsTable.Columns["TravelerID"] },
                new DataColumn[] { markersTable.Columns["TravelerID"] });

            // Markers Relations
            set.Relations.Add("MarkerMaterials",
                new DataColumn[] { markersTable.Columns["TravelerID"], markersTable.Columns["Name"] },
                new DataColumn[] { markerMaterialsTable.Columns["TravelerID"], markerMaterialsTable.Columns["MarkerName"] });

            set.Relations.Add("ModelsFromMaterial",
                new DataColumn[] { markerMaterialsTable.Columns["TravelerID"], markerMaterialsTable.Columns["MarkerName"], markerMaterialsTable.Columns["SpreadSet"] },
                new DataColumn[] { markerModelsTable.Columns["TravelerID"], markerModelsTable.Columns["MarkerName"], markerModelsTable.Columns["SpreadSet"] });

            // Marker Status Relations
            set.Relations.Add("MarkerStatus",
                new DataColumn[] { markersTable.Columns["TravelerID"], markersTable.Columns["Name"] },
                new DataColumn[] { NestingSummary.Columns["TravelerID"], NestingSummary.Columns["Marker"] });
        }

        private System.Data.DataTable CreateCutTicketsTable(string TicketsPrefix)
        {
            var ret = new System.Data.DataTable() { TableName = TicketsPrefix + "CutTickets" };

            // Add the columns
            //ret.Columns.Add(new DataColumn() { ColumnName = "Models" }); Relationship
            //ret.Columns.Add(new DataColumn() { ColumnName = "Markers" }); Relationship
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "FabricRequired" });

            return ret;
        }
        private System.Data.DataTable CreateModelsTable(string TicketsPrefix)
        {
            var ret = new System.Data.DataTable() { TableName = TicketsPrefix + "CtModels" };

            // Add the columns
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Name" });
            
            ret.Columns.Add(new DataColumn() { ColumnName = "PcQty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Qty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Perimeter" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Material" });
            ret.Columns.Add(new DataColumn() { ColumnName = "MaterialAmount" });
            //ret.Columns.Add(new DataColumn() { ColumnName = "Material" }); Relationship

            return ret;
        }
        private System.Data.DataTable CreateModelMaterialsTable(string TicketsPrefix)
        {
            var ret = new System.Data.DataTable() { TableName = TicketsPrefix + "CtModelMats" };

            // Add the columns
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "ModelName" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Name" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Qty" });

            return ret;
        }
        private System.Data.DataTable CreateMarkersTable(string TicketsPrefix)
        {
            var ret = new System.Data.DataTable() { TableName = TicketsPrefix + "CtMarkers" };

            // Add the columns
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Name" });
            
            ret.Columns.Add(new DataColumn() { ColumnName = "PlannedWidth" });
            ret.Columns.Add(new DataColumn() { ColumnName = "TotalPlannedFabricIN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "TotalActualFabricIN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "ActualWidth" });
            ret.Columns.Add(new DataColumn() { ColumnName = "PlannedPlyLengthIN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "ActualPlyLengthIN" });
            ret.Columns.Add(new DataColumn() { ColumnName = "TargetUtilization" });
            //ret.Columns.Add(new DataColumn() { ColumnName = "Material" }); Relationship

            return ret;
        }
        private System.Data.DataTable CreateMarkerModelsTable(string TicketsPrefix)
        {
            var ret = new System.Data.DataTable() { TableName = TicketsPrefix + "CtMarkerModels" };

            // Add the columns
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "MarkerName" });
            ret.Columns.Add(new DataColumn() { ColumnName = "SpreadSet" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Name" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Parent" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc1" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Desc2" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Qty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "QtyTotal" });

            return ret;
        }
        private System.Data.DataTable CreateMarkerMaterialsTable(string TicketsPrefix)
        {
            var ret = new System.Data.DataTable() { TableName = TicketsPrefix + "CtMarkerMats" };

            // Add the columns
            ret.Columns.Add(new DataColumn() { ColumnName = "TravelerID" });
            ret.Columns.Add(new DataColumn() { ColumnName = "MarkerName" });
            ret.Columns.Add(new DataColumn() { ColumnName = "Name" });
            ret.Columns.Add(new DataColumn() { ColumnName = "SpreadQty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "PlyQty" });
            ret.Columns.Add(new DataColumn() { ColumnName = "SpreadSet" });
            ret.Columns.Add(new DataColumn() { ColumnName = "TotalPlys" });

            ret.Columns.Add(new DataColumn() { ColumnName = "MaterialDesc" });
            ret.Columns.Add(new DataColumn() { ColumnName = "MaterialPN" });
            //ret.Columns.Add(new DataColumn() { ColumnName = "Models" }); Relationship

            return ret;
        }

        #region Cut Ticket Classes
        public class CutTicket
            {
                public string ID { get; set; }
                public List<ctInputModels> Input_Models { get; set; }
                public List<string> FabricCodes { get; set; }
                public List<ctModels> Models { get; set; }
                public List<ctMarkers> Markers { get; set; }
                public double FabricRequired { get; set; }
                public DateTime DateRequested { get; set; }
                public string ScheduleName { get; set; }
            }
        public class ctInputModels
        {
            public string Name { get; set; }
            public List<ctFabricCodes> FabricCodes { get; set; }

        }
        public class ctFabricCodes
        {
            public string FabricCode { get; set; }
            public string SizeLine { get; set; }
            public List<string> SizeID { get; set; }
        }
        public class ctModels
        {
            public string Name { get; set; }
            public int PcQty { get; set; }
            public int Qty { get; set; }
            public double Perimeter { get; set; }
            public List<ctModelMat> Material { get; set; }
            public string SizeID { get; set; }
            public string SizeLine { get; set; }

        }
        public class ctModelMat
        {
            public string Name { get; set; }
            public int Qty { get; set; }
        }
        public class ctMarkers
        {
            public string Name { get; set; }
            public double MatWidth { get; set; }
            public double PlannedFabricIN { get; set; }
            public double ActualFabric { get; set; }
            public double TargetUtilization { get; set; }
            public List<ctMarkerModels> Models { get; set; }
            public List<ctMarkerMaterial> Material { get; set; }
        }
        public class ctMarkerModels
        {
            public string Name { get; set; }
            public int Qty { get; set; }
            public int QtyTotal { get; set; }
            public int SpreadSet { get; set; }
        }
        public class ctMarkerMaterial
        {
            public string Name { get; set; }
            public int SpreadQty { get; set; }
            public int SpreadSet { get; set; }
            public int PlyQty { get; set; }
            public double PlyLength { get; set; }
            public int TotalPlys { get; set; }
        }
        #endregion
    }
}
