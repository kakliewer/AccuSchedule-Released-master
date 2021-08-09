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
using Newtonsoft.Json;
using System.Windows.Controls;
using AccuSchedule.UI.Methods;
using System.IO;
using System.Linq.Dynamic.Core;
using AccuSchedule.Shared.Models.Orders;
using ClosedXML.Report.Utils;
using ReactiveUI;
using SuperXML;
using AccuSchedule.UI.Views.Dialogs;
using AccuSchedule.UI.ViewModels;

namespace AccuSchedule.UI.Plugins.Tools
{
    public class GenerateXMLTool : ToolPlugin
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

        private string TicketsPrefix { get; set; } = string.Empty;


        public DataSet GenerateXML(DataSet _set, Action<DataSet> Generate, string XMLTemplate, Button _BrowseForFile, string AccuplanOptionsList, Button _BrowseForFile2, string SaveLocation, Button _BrowseForDir)
        {

            if (_set == null) return null;
            if (!string.IsNullOrEmpty(SaveLocation) && !string.IsNullOrEmpty(AccuplanOptionsList))
            {

                if (_set.DataSetName.Contains("Schedule"))
                    TicketsPrefix = _set.DataSetName.Replace("Schedule", "");

                var ticketsTable = GetTableWithNameEndingWith(_set, "Travelers");

                if (ticketsTable != null) // Make sure the data set is valid
                {
                    Options = LoadOptionsList(AccuplanOptionsList);
                    var xmlTemplate = GetXMLTemplate(XMLTemplate);

                    // Process if options and xml template loaded successfully
                    if (Options.Any() && !string.IsNullOrEmpty(xmlTemplate))
                        ProcessTickets(_set, ticketsTable, XMLTemplate, SaveLocation);
                }
            }

            return _set;
        }


        private string _StorageAreaDrive { get; set; } = "S";
        private string _StorageArea { get; set; } = "G-STORAGE";

        public HashSet<(string opt, string desc)> Options { get; set; }


        private async void ProcessTickets(DataSet _set, DataTable tickets, string xmlTemplate, string xmlOutputDir)
        {
            var bOverwrite = false;
            foreach (var ticket in tickets.AsEnumerable())
            {
                var ticketID = ticket.Field<object>("TravelerID").ToString();

                // Create anonymous list of Storage Area's for XML Template
                var storageList = CreateStorages();
                var storagesDef = new[] { new { storagearea = "", driveletter = "", area = "" } };
                var storages = Translate(storagesDef, storageList);

                // Create anonymous list of Models for XML Template
                var modelsList = CreateModels(ticket);
                var modelsDef = new[] { new { partnumber = "", fabcodescombined = "", sizeid = "", size = "", storagearea = "", fabriccodes = new[] { "" }, options = new[] { "" }, hasoptions = false } };
                

                if (modelsList.Parts.Any())
                {
                    // Create anonymous list of Cut Plans for XML Template
                    var cutPlansList = CreateCutPlans(modelsList.Parts);
                    var cutPlansDef = new[] { new { fabriccode = "", vinylcolors = new[] { "" }, vinylwidth = "", vinylcost = "", markersettings = "", spreadsettings = "", costsettings = "", ordersettings = "" } };
                    var cutPlans = Translate(cutPlansDef, cutPlansList);

                    // Create anonymous list of Planned Vinyl for XML Template
                    var plannedVinylList = CreatePlannedVinyl(modelsList.Parts, modelsList.Models);
                    var plannedVinylDef = new[] { new { vinylcolor = "", sizeid = "", qty = "", min = "", max = "" } };
                    var plannedVinyl = Translate(plannedVinylDef, plannedVinylList);

                    

                    // Finalize the models
                    var models = Translate(modelsDef, modelsList.Models);

                    // Create the XML's
                    using (StringReader xmlTemplateReader = new StringReader(File.ReadAllText(xmlTemplate)))
                    {
                        var compiledXML = new Compiler()
                            .AddKey("sonum", ticketID)
                            .AddKey("user", Environment.UserName)
                            .AddKey("datereceived", DateTime.Now)
                            .AddKey("cutdate", DateTime.Now)
                            .AddKey("wocomments", "")
                            .AddKey("currentdate", DateTime.Now)
                            .AddKey("storageareas", storages)
                            .AddKey("models", models)
                            .AddKey("cutplans", cutPlans)
                            .AddKey("plannedvinyl", plannedVinyl)
                            .CompileXml(xmlTemplateReader);

                        if (bOverwrite == false && File.Exists(xmlOutputDir + "\\" + ticketID + ".XML"))
                        {
                            MessageBoxResult result = MessageBox.Show("Existing Traveler ID's found. Would you like to overwrite?", "Travelers Found!", MessageBoxButton.YesNoCancel);
                            switch (result)
                            {
                                case MessageBoxResult.Yes:
                                    File.WriteAllText(xmlOutputDir + "\\" + ticketID + ".XML", compiledXML);
                                    break;
                                case MessageBoxResult.No:
                                    break;
                                case MessageBoxResult.Cancel:
                                    break;
                            }
                        }
                        else
                        {
                            File.WriteAllText(xmlOutputDir + "\\" + ticketID + ".XML", compiledXML);
                        }
                        
                    }
                }

            }
        }
        
        private static Boolean DelimetedArrayContains(string[] array, string toFind)
        {
            foreach (var item in array)
                if (!string.IsNullOrEmpty(item) && item.Contains('|'))
                {
                    var splitter = item.Split('|');
                    foreach (var splitItem in splitter)
                        if (splitItem == toFind || splitItem.Substring(0, splitItem.Length - 2) == toFind)
                            return true;
                }
                else
                    if (!string.IsNullOrEmpty(item) && item == toFind || !string.IsNullOrEmpty(item) && item.Substring(0, item.Length - 2) == toFind)
                        return true;

            return false;
        }

        List<PlannedVinyl> CreatePlannedVinyl(IEnumerable<dynamic> Parts, IEnumerable<Model> models)
        {
            var list = new List<PlannedVinyl>();

            // Loop through the models and find the parts
            foreach (var model in models)
            {
                var pn = model.partnumber;
                var partMatches = Parts.Where(w => w.PN == pn);
                var totalQty = partMatches.GroupBy(g => new[] { pn = g.PN }).Select(s => s.Sum(add => add.Qty)).Sum();

                // Loop through parts and materials
                foreach (var part in partMatches)
                {
                    var materials = part.Materials as IEnumerable<DataRow>;


                    foreach (DataRow material in materials)
                    {
                        PlannedVinyl item = new PlannedVinyl();
                        item.vinylcolor = string.Format("{0}, {1}", GetUsedColor(material?.Field<object>("Desc1")?.ToString()), material?.Field<object>("MaterialPN")?.ToString());
                        item.qty = totalQty;
                        item.min = totalQty;
                        item.max = totalQty;
                        item.sizeid = model.sizeid;

                        // Check if exists in List, if so increment otherwise add
                        var dupeCheck = list.Where(w => w.sizeid == item.sizeid && w.vinylcolor.Contains(material?.Field<object>("MaterialPN")?.ToString())).FirstOrDefault();
                        if (dupeCheck == null)
                            list.Add(item);
                    }
                    

                }

            }

            


            return list;
        }

        private List<CutPlan> CreateCutPlans(IEnumerable<dynamic> Parts)
        {
            List<CutPlan> list = new List<CutPlan>();

            // Get unique list of fabric codes
            var materials = Parts.Select(s => s.Materials as IEnumerable<DataRow>);
            

            var uniqueFabricCodes = new HashSet<string>();

                foreach (var material in materials)
                {
                    foreach (var mat in material)
                        uniqueFabricCodes.Add(mat?.Field<object>("AccumarkFabCode")?.ToString());

                    foreach (var fabCode in uniqueFabricCodes)
                    {
                        // Format the color list
                        var colors = from pm in material
                                     where pm.Field<object>("AccumarkFabCode").ToString() == fabCode && !string.IsNullOrEmpty(pm.Field<object>("MaterialPN").ToString())
                                     select new { partNum = pm.Field<object>("MaterialPN").ToString(), desc = pm.Field<object>("Desc1").ToString() };

                        HashSet<string> colorList = new HashSet<string>();
                        foreach (var c in colors?.Distinct())
                            colorList.Add(string.Format("{0}, {1}", GetUsedColor(c.desc), c.partNum));

                        // Add new color
                        if (colorList.Any())
                        {
                            CutPlan item = new CutPlan();
                            item.fabriccode = fabCode;
                            item.vinylcolors = colorList.ToArray();
                            item.vinylwidth = "65.5"; //? Somethign to make user configurable later?
                            item.vinylcost = "0";

                            item.OrderSettings = "G-STORE"; // Unique per cutplan but keeping all same for now.
                            item.MarkerSettings = "SCHOOLBUS"; // Unique per cutplan but keeping all same for now.
                            item.SpreadSettings = "Default"; // Unique per cutplan but keeping all same for now.
                            item.CostSettings = "Default"; // Unique per cutplan but keeping all same for now.

                            if (!list.Any(a => a.fabriccode == fabCode && a.vinylcolors.SequenceEqual(colorList.ToArray())))
                                list.Add(item);
                        }

                    }
                }

                    
            

            return list.Distinct().ToList();
        }

        private static string GetUsedColor(string Desc)
        {
            if (Desc.Contains(" "))
            {
                string[] color = Desc.Split(' '); // Extract color from Desc1
                string usedColor = string.Empty;
                if (color.Any())
                {
                    if (color.Count() > 1)
                    {
                        usedColor = color[1];
                    }
                    else { usedColor = Desc; }
                }

                return usedColor;
            }
            else
            {
                return string.Empty;
            }
        }


        private List<Storage> CreateStorages()
        {
            Storage item = new Storage();
            item.storagearea = _StorageAreaDrive + ":" + _StorageArea;
            item.driveletter = _StorageAreaDrive;
            item.area = _StorageArea;

            List<Storage> list = new List<Storage>();
            list.Add(item);

            return list;

        }
        private  (List<Model> Models, IEnumerable<dynamic> Parts) CreateModels(DataRow TravelerRow)
        {
            List<Model> list = new List<Model>();

            var parts = GetPartsOnOrder(TravelerRow);


            foreach (var part in parts)
            {
                HashSet<Model> models = CreatePartForModel(part); // Increments sizeIDer accordingly

                // Check if part exists before adding
                foreach (var model in models)
                    if (!list.Contains(model))
                        list.Add(model);
            }

            // Group by "partnumber" then assign sizeID
            var ret = list.GroupBy(g => new { PN = g.partnumber })
                .Select((s, idx) => new Model
                {
                    fabriccodes = s.Select(sel => sel.fabriccodes).First(),
                    options = s.Select(sel => sel.options).First(),
                    partnumber = s.Key.PN,
                    size = s.Select(sel => sel.size).First(),
                    storagearea = s.Select(sel => sel.storagearea).First(),
                    hasXRefs = s.Select(sel => sel.hasXRefs).First(),
                    sizeid = idx + 1,
                    qty = s.Sum(sel => sel.qty)
                }); 

            return (ret.ToList(), parts);
        }

        private HashSet<Model> CreatePartForModel(dynamic partOnOrder)
        {

            HashSet<Model> modelList = new HashSet<Model>();

            // Loop through the materials and get each of the fabric codes
            var fabCodeList = new HashSet<string>();
            var modelTabs = new HashSet<string>();

            var PNQty = partOnOrder.Qty as int?;
            var materialRows = partOnOrder.Materials as IEnumerable<DataRow>;
            var PNItem = partOnOrder.PN as string;
            var xRefParent = partOnOrder.xRefParents as string[];

            var pns = new List<string>();
            var hasXref = false;
            if (xRefParent != null && !string.IsNullOrEmpty(xRefParent.FirstOrDefault()))
                hasXref = true;

            foreach (var mat in materialRows)
            {
                fabCodeList.Add("1"); // Always adds default pieces
                var accTabCode = mat?.Field<object>("AccumarkTabCode")?.ToString();
                var accFabCode = mat?.Field<object>("AccumarkFabCode")?.ToString();
                fabCodeList.Add(GetFormattedFabricCode(accTabCode, accFabCode));

                if (!string.IsNullOrEmpty(accTabCode)) // dont add if empty so 
                    modelTabs.Add(accTabCode);

            }


            // Only add if fabric codes were found
            if (fabCodeList != null && fabCodeList.Any())
            {
                Model model = new Model();
                model.partnumber = PNItem;
                model.fabriccodes = fabCodeList.ToArray();
                model.size = 1; // Always 1
                model.storagearea = _StorageAreaDrive + ":" + _StorageArea;
                model.options = modelTabs.ToArray();
                model.qty = PNQty == null ? 0 : Convert.ToInt32(PNQty);
                model.hasXRefs = hasXref;

                modelList.Add(model);
            }
            else
            {
                //Log.Error("Could not find Fabric Codes for {0}. Was not added to Cut Plan.", OrderPart.PartNumber);
            }
            

            return modelList;
        }

        /// <summary>
        /// Returns all parts of a traveler if it has material and a labor route with "cut" in the description.
        /// </summary>
        /// <param name="TravelerRow">Traveler Row</param>
        /// <returns>Dynamic { PN (string), Qty (int), Materials (IEnumerable<DataRow>), and Labors (IEnumerable<DataRow>) }</DataRow></Datarow></returns>
        private static IEnumerable<dynamic> GetPartsOnOrder(DataRow TravelerRow) =>
             GetPartsFromTravelerRow(TravelerRow)
                .Where(w =>
                    !string.IsNullOrEmpty(w.Field<object>("PN").ToString()) && !w.Field<object>("PN").ToString().Contains("No Child!")
                    && w.Table.ColumnNames("xRef", false).FirstOrDefault() != null 
                        ? w.Field<object>("PN").ToString().Trim() != w.Field<object>("xRefParent").ToString().Trim()
                        : true
                    && w.GetChildRows("LaborsForPart").Where(l => l.Field<object>("Desc").ToString().ToLower() == "labor - cut" || l.Field<object>("Desc").ToString().ToLower() == "labor-cut").Any()
                    && w.GetChildRows("MaterialsForPart").Any())
                .Select(s => new
                {
                    PN = s.Field<object>("PN").ToString().Length > 3 && s.Table.ColumnNames("xRef", false).FirstOrDefault() != null && s.Field<object>("PN").ToString().Trim() != s.Field<object>("xRefParent").ToString().Trim()
                    ? s.Field<object>("PN").ToString().Substring(0, s.Field<object>("PN").ToString().Length - 2) 
                    : s.Field<object>("PN").ToString(),
                    xRefParents = s.Table.ColumnNames().Contains("xRefParent") ? s.Field<object>("xRefParent")?.ToString() : null,
                    Qty = Convert.ToInt32(s.Field<object>("Qty").ToString()),
                    Materials = GetMaterialsFromPartRow(s),
                    Labors = GetLaborsFromPartRow(s)
                })
                .GroupBy(g => g.PN)
                .Select(s => new
                {
                    PN = s.Key,
                    xRefParents = s.Select(x => x.xRefParents).Distinct().ToArray(),
                    Qty = s.Sum(a => a.Qty),
                    Materials = s.Select(x => x.Materials).SelectMany(x => x),
                    Labors = s.Select(x => x.Labors).SelectMany(x => x)
                });

        


        private string GetFormattedFabricCode(string BOMCode, string FabricCode)
        {

            // Convert any "A" (primary) to Fabric Code only.
            if (!string.IsNullOrEmpty(BOMCode) && BOMCode.ToLower() == "a") 
                return FabricCode;

            // Convert any "B" to Fabric Code # + "B".
            if (!string.IsNullOrEmpty(BOMCode) && BOMCode.ToLower() == "b")
            {
                if (!FabricCode.ToLower().Contains("b"))
                    return FabricCode + "B";
                else
                    return FabricCode;
            }

            // If other tab than "A" or "B" (primary or secondary).
            if (!string.IsNullOrEmpty(BOMCode) && BOMCode.ToLower() != "a"
                && BOMCode.ToLower() != "b")
            {
                // Check if "primary" or "secondary" is present in option description
                var opt = Options.Where(w => w.opt.ToLower() == BOMCode.ToLower())
                    .Select(s => s).FirstOrDefault();

                if (!string.IsNullOrEmpty(opt.desc))
                {
                    if (opt.desc.ToLower().Contains("primary"))
                    { // Add as fabric code only
                        return FabricCode;
                    }
                    else if (opt.desc.ToLower().Contains("secondary"))
                    { // Add as fabric code + B
                        if (!FabricCode.ToLower().Contains("b"))
                            return FabricCode + "B";
                        else
                            return FabricCode;
                    }
                    else
                    { // Unique fabric, plan on it's own... add FabCode + TabName
                        if (!FabricCode.ToLower().Contains(BOMCode.ToLower()))
                            return FabricCode + BOMCode;
                        else
                            return FabricCode;
                    }
                }

            }

            // If no option is found
            if (string.IsNullOrEmpty(BOMCode))
            {
                return FabricCode;
            }

            return null;
        }




        private static DataRow[] GetOrdersFromTravelerRow(DataRow travelerRow) => travelerRow.GetChildRows("Orders");
        private static DataRow[] GetPartsFromTravelerRow(DataRow travelerRow) => travelerRow.GetChildRows("Parts");
        private static DataRow[] GetPartsFromOrderRow(DataRow orderRow) => orderRow.GetChildRows("PartsOnOrder");
        private static DataRow[] GetMaterialsFromPartRow(DataRow partRow) => partRow.GetChildRows("MaterialsForPart");
        private static DataRow[] GetLaborsFromPartRow(DataRow partRow) => partRow.GetChildRows("LaborsForPart");


#region Accuplan XML Template DTOs
        public class Storage
        {
            public string storagearea { get; set; }
            public string driveletter { get; set; }
            public string area { get; set; }
        }

        public class Model
        {
            //{'partnumber':'G-BN3-39XX', 'fabriccode':'1', 'sizeid':'1', 'size':'1', 'storagearea':'S:GENESIS'}
            public string partnumber { get; set; }
            public string[] fabriccodes { get; set; }
            public string fabcodescombined { get { return string.Join(",", fabriccodes); } }
            public int sizeid { get; set; }
            public int size { get; set; }
            public string storagearea { get; set; }
            public bool hasoptions { 
                get 
                {
                    if (options == null || !options.Any() || options.Count() == 0) return false;
                    return true;
                } 
            }
            public int qty { get; set; }
            public bool hasXRefs { get; set; }

            public string[] options { get; set; }
        }
        public class CutPlan
        {
            //[{'fabriccode':'1', 'vinylcolor':'BLACK', 'vinylwidth':'65.5', 'vinylcost':'0'}]
            public string fabriccode { get; set; }
            public string[] vinylcolors { get; set; }
            public string vinylwidth { get; set; }
            public string vinylcost { get; set; }

            public string OrderSettings { get; set; } = "G-STORE";
            public string MarkerSettings { get; set; } = "SCHOOLBUS";
            public string CostSettings { get; set; } = "ACCUPLAN COSTING";
            public string SpreadSettings { get; set; } = "Default";
        }
        public class PlannedVinyl
        {
            //{'vinylcolor':'BLACK', 'sizeid':'1', 'qty':'24', 'min':'24', 'max':'24'}
            public string vinylcolor { get; set; }
            public int sizeid { get; set; }
            public int qty { get; set; }
            public int min { get; set; }
            public int max { get; set; }

        }
#endregion

        private DataTable GetTableWithNameEndingWith(DataSet _set, string what)
        {
            if (_set?.Tables == null || _set?.Tables.Count == 0) return null;

            foreach (DataTable table in _set.Tables)
                if (table.TableName.EndsWith(what) && table.ColumnNames().Contains("TravelerID"))
                    return table;

            return null;
        }

        public string GetXMLTemplate(string XMLTemplateFile)
        {
            var ret = string.Empty;
            if (File.Exists(XMLTemplateFile))
                ret = File.ReadAllText(XMLTemplateFile);
            return ret;
        }

        public HashSet<(string opt, string desc)> LoadOptionsList(string optionsListFile)
        {
            var optionsList = new HashSet<(string opt, string desc)>();

            ExcelInputHandler ei = new ExcelInputHandler();

            var iXLtables = ei.ExtractTables(optionsListFile);

            foreach (var table in iXLtables)
            {
                var optCol = table.HeadersRow().Cells().Where(a => a.Value.ToString().ToLower() == "opt").FirstOrDefault()?.Address.ColumnLetter;
                var descCol = table.HeadersRow().Cells().Where(a => a.Value.ToString().ToLower() == "description").FirstOrDefault()?.Address.ColumnLetter;


                if (optCol != null && descCol != null)
                    foreach (var row in table.Rows().AsEnumerable())
                    {
                        string optVal = row.Cell(optCol).Value as string;
                        string descVal = row.Cell(descCol).Value as string;

                        if (optVal.ToLower() != "opt" && descVal.ToLower() != "description")
                            optionsList.Add((optVal, descVal));
                    }

            }


            return optionsList;
        }





        private string FormatPN(string PartNumber)
        {
            if (PartNumber.Contains("-"))
            {
                // Check for correct format and remove material codes if found
                var pnSplit = PartNumber.Split('-');
                if (pnSplit[1].Length == 3 && pnSplit[2].Length == 4)
                {
                    pnSplit[2] = pnSplit[2].Substring(0, 2);

                    return string.Join("-", pnSplit);
                }
            }

            return PartNumber;
        }

        /// <summary>
        /// Serializes a defined object into JSON then Descerializes into an Anonymous type for SuperXML
        /// </summary>
        /// <param name="def">Definition of anonymous type</param>
        /// <param name="obj">Object to be converted to anonymous</param>
        /// <returns></returns>
        public dynamic Translate(dynamic def, dynamic obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            var translation = JsonConvert.DeserializeAnonymousType(json, def);

            return translation;
        }


    }
}
