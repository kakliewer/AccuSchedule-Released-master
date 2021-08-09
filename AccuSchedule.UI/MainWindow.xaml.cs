using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using AccuSchedule.UI.Extensions;
using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Methods;
using AccuSchedule.UI.Models;
using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.Plugins.Tools;
using AccuSchedule.UI.Test;
using AccuSchedule.UI.ViewModels;
using AccuSchedule.UI.ViewModels.VisualEditor;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
using AccuSchedule.UI.Views;
using AccuSchedule.UI.Views.Dialogs;
using AccuSchedule.UI.Views.VisualEditor;
using Antlr.Runtime.Tree;
using Castle.Core.Smtp;
using ClosedXML.Report.Utils;
using DynamicData;
using Dynamitey;
using Jint.Runtime;
using MahApps.Metro.Controls;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using MoreLinq;
using Newtonsoft.Json.Linq;
using Ninject;
using NodeNetwork.Toolkit;
using NodeNetwork.Toolkit.ValueNode;
using NodeNetwork.ViewModels;
using NodeNetwork.Views;
using ReactiveUI;
using DataTableExtensions = AccuSchedule.UI.Extensions.DataTableExtensions;
using Theme = MaterialDesignThemes.Wpf.Theme;

namespace AccuSchedule.UI
{
    
    public partial class MainWindow : MetroWindow, IProcessingWindow, IViewFor<MainViewModel>
    {
        
        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(MainViewModel), typeof(MainWindow), new PropertyMetadata(null));

        public MainViewModel ViewModel
        {
            get => (MainViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (MainViewModel)value;
        }
        #endregion

        public bool isProcessing { get; set; } = false;
        public ToolProcessor Tools { get; }
        public ViewBuilderBase<DockPanel, StackPanel, DockPanel, TabControl, TabItem> viewBuilder { get; set; }
        public DockPanel WindowView { get; set; }
        public string ProjectName { get; set; } = "Unsaved Project.";
        public MetroWindow ProcessingWindow => this;

        public bool isLastColWidthForPropAuto { get; set; }
        public double lastColWidthForProps { get; set; }
        public bool initialWindowLocate { get; set; } = false;

        public DefaultNodeViewModel LastNodeSelected { get; set; }

        public DataSet Data { get; set; }

        public System.Windows.Forms.Timer SearchTextBoxTimer { get; set; } = null;

        public MainWindow()
        {
            
            InitializeComponent();

            Tools = new ToolProcessor(this);

            LoadSettings();


            LoadNetworkView();
            isLoading = false;
        }


        private void LoadSettings()
        {
            if (Properties.Settings.Default.LastTableWinHeight > 0) UpperSplitRow.Height = new GridLength(Properties.Settings.Default.LastTableWinHeight);
            if (Properties.Settings.Default.LastNodeListWidth > 0) LeftSplitter.Width = new GridLength(Properties.Settings.Default.LastNodeListWidth);
            if (Properties.Settings.Default.LastPropWinWidth > 0) PropWidthLock.Width = new GridLength(Properties.Settings.Default.LastPropWinWidth);
            if (Properties.Settings.Default.LastPropWinHeight > 0) PropWinHeight.Height = new GridLength(Properties.Settings.Default.LastPropWinHeight);

            if (Properties.Settings.Default.LastWindowPosition_Left != 0) this.Left = Properties.Settings.Default.LastWindowPosition_Left;
            if (Properties.Settings.Default.LastWindowPosition_Top != 0) this.Top = Properties.Settings.Default.LastWindowPosition_Top;

            
        }

        public bool isTemplatePrj { get; set; }
        public bool isLoading { get; set; } = true;
        

        public void LoadNetworkView()
        {
            ToolProcessor.OnUpdate += ViewModel_OnTableUpdated;
            ToolProcessor.IsExecuting += ToolProcessor_IsExecuting;
            ToolProcessor.DoneExecuting += ToolProcessor_DoneExecuting;

            // Assign the network to the view model
            this.WhenActivated(d =>
            {
                this.OneWayBind(ViewModel, vm => vm.Network, v => v.network.ViewModel).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.NodeList, v => v.nodeList.ViewModel).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.Tables, v => v.tables.ViewModel).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.Scope, v => v.scope.ViewModel).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.PropView, v => v.props.ViewModel).DisposeWith(d);
            });

            this.ViewModel = new MainViewModel();
            this.ViewModel.OnTableAdded += ViewModel_OnTableAdded;
            this.ViewModel.OnTableUpdated += ViewModel_OnTableUpdated;
            this.ViewModel.OnTableRemoved += ViewModel_OnTableRemoved;
            this.ViewModel.OnScopeUpdated += ViewModel_OnScopeUpdated;
            this.ViewModel.OnVoidUpdated += ViewModel_OnVoidUpdated;

            nodeList.CVS.GroupDescriptions.Add(new PropertyGroupDescription("Category"));

            this.props.LostPropFocus += PropsItem_LostPropFocus;

            // Load the Tool Plugins through Ninject's kernel
            var theApp = Application.Current as App;
            foreach (var toolPlugin in theApp.kernel.GetAll<IToolPlugin>())
            {
                // Check if we should only load specific types
                if (toolPlugin.TypesToLoad.Any())
                    foreach (var loadType in toolPlugin.TypesToLoad)
                        Tools.AddToolNodes(ViewModel.NodeList, toolPlugin, loadType);
                else
                    Tools.AddToolNodes(ViewModel.NodeList, toolPlugin, null);
            }


        }

        private void ToolProcessor_DoneExecuting()
        {
            isProcessing = false;
        }

        private void ToolProcessor_IsExecuting()
        {
            isProcessing = true;
        }

        private void ViewModel_OnVoidUpdated(IDictionary<ExportNode, IEnumerable<object>> VoidNodeAndResults) 
        {
            // Loop through, add to the void payload, then process to ViewModel_OnScopeUpdated
            if (!VoidNodeAndResults.Any()) return;

            var objList = new HashSet<string>();
            foreach (var item in VoidNodeAndResults)
            {
                if (item.Value.Any())
                {
                    item.Key.ValueEditor = new VoidEditorViewModel(item.Value);
                }
            }

            

        }


        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.RightWindowCommands.FindChild<TextBox>("txtSearch").Text = "Search View...";
        }


        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb.Text == "Search View...") tb.Text = string.Empty;
        }
        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = "Search View...";
            }
        }
        private void txtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var grid = GridExtensions.FindActiveGrid(tables.tabs);
                try
                {
                    if (grid != null) 
                    { 
                        var dataview = grid.ItemsSource as DataView;
                        if (string.IsNullOrEmpty(txtSearch.Text))
                            dataview.RowFilter = "";
                        else
                            dataview.RowFilter = DataTableExtensions.CreateTableSearchQuery(dataview.Table, txtSearch.Text);
                    }
                }
                catch (Exception)
                {
                }
                if (SearchTextBoxTimer != null)
                {
                    SearchTextBoxTimer.Stop();
                    SearchTextBoxTimer.Dispose();
                    SearchTextBoxTimer = null;
                }
                return;
            }
            else
            {
                if (SearchTextBoxTimer != null)
                {
                    if (SearchTextBoxTimer.Interval < 750)
                        SearchTextBoxTimer.Interval += 750;
                }
                else
                {
                    SearchTextBoxTimer = new System.Windows.Forms.Timer();
                    SearchTextBoxTimer.Tick += new EventHandler(SearchTextBoxTimer_Tick);
                    SearchTextBoxTimer.Interval = 500;
                    SearchTextBoxTimer.Start();
                }
            }
        }
        private void SearchTextBoxTimer_Tick(object sender, EventArgs e)
        {
            var grid = GridExtensions.FindActiveGrid(tables.tabs);
            try
            {
                if (grid != null)
                {
                    var dataview = grid.ItemsSource as DataView;
                    if (string.IsNullOrEmpty(txtSearch.Text))
                        dataview.RowFilter = "";
                    else
                        dataview.RowFilter = DataTableExtensions.CreateTableSearchQuery(dataview.Table, txtSearch.Text);
                }
            }
            catch (Exception)
            {
            }
            

            SearchTextBoxTimer.Stop();
            SearchTextBoxTimer.Dispose();
            SearchTextBoxTimer = null;
        }
        public async Task LoadNewWindow(bool askToSave = true)
        {
            if (askToSave)
            {
                var dialogResults = await new QuestionDialog().ShowAsDialog(new QuestionDialogViewModel("Would you like to Save first?"), dh);
                if (dialogResults != null)
                {
                    await SaveDialog();
                }
            }

            var mainWin = new MainWindow();
            mainWin.Show();

            this.Close();
        }

        private async void ContextItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                switch (menuItem?.Header)
                {
                    case "New Window":
                        LoadNewWindow();
                        break;
                    case "Import Excel File":
                        ExcelInputHandler ei = new ExcelInputHandler();

                        var file = FileExtensions.OpenFileOrNull("Open Excel File", "xlsx", "Excel file(*.xlsx) | *.xlsx");
                        var splitter = file?.FileName?.Split('\\');
                        var preview =
                            new Preview(ei.ExtractTables(file?.FileName)
                                , file?.FileName
                                , this);
                        preview.Show();
                        isLoading = false;
                        break;
                    case "Save":
                        //await Save();
                        await SaveDialog();
                        break;
                    case "Load":
                        if (LastNodeSelected != null)
                            await LoadNewWindow();

                        Load();
                        break;
                    case "Settings":
                        List<ISetting> appSettings = ((App)Application.Current).SettingsContainer;

                        AddTestCategories();
                        var Settings = new AppSettings(appSettings);
                        Settings.Show();
                        break;
                    case "Light Theme":
                        SetLightTheme();
                        break;
                    default:
                        break;
                }
            }

            var results = e as ExecutedRoutedEventArgs;
            var uiCommand = results?.Command as RoutedUICommand;
            if (uiCommand?.Text == "ShowNodeChain") SetDisplayNodeChain();
            if (uiCommand?.Text == "DarkTheme") SetLightTheme();


            e.Handled = true;
        }
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn.ContextMenu != null)
            {
                ContextMenu contextMenu = btn.ContextMenu;
                contextMenu.PlacementTarget = btn;
                contextMenu.IsOpen = true;
            }
            e.Handled = true;
        }
        
        /// <summary>
        /// Obsoleted in favor of SaveDialog()
        /// </summary>
        /// <returns></returns>
        public async Task Save()
        {
            // Get the Project Name and Directory via Dialog
            var dialogResults = await new SaveProjectDialog().ShowAsDialog(new SaveProjectDialogViewModel(), dh);

            // Loop through each node and save the object on order.
            if (dialogResults == null) return;

            var isTemplate = false;
            if (dialogResults.SaveAs == SaveProjectDialogViewModel.SaveAsEnum.Template.ToString())
                isTemplate = true;

            var ext = string.Empty;
            if (isTemplate) ext = ".accTmplt";
            else ext = ".accPrj";

            ProjectName = dialogResults.ProjectName + ext;
            this.Title = "ACCUSCHEDULE - " + ProjectName;

            // If saving as Template, the XMLPayload is not saved. This signals the Load function that it should load the table from the filename of the startnode.
            var projectNodes = GetNodesAsProjectNodes(isTemplate);

            var project = new ProjectSettings()
            {
                Name = dialogResults.ProjectName,
                LastSaveDirectory = dialogResults.FilePath,
                Nodes = projectNodes
            };

            project.WriteToBinaryFile(string.Format(@"{0}\{1}.{2}", dialogResults.FilePath, dialogResults.ProjectName, isTemplate ? "accTmplt" : "accPrj"));
        }
        public async Task SaveDialog()
        {
            var dir = @"\\Store\hsmc$\213\dept\ENG\import\";
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = dir;
            saveFileDialog.AddExtension = true;
            saveFileDialog.Filter = "Template file (*.accTmplt)|*.accTmplt|Profile file (*.accPrj)|*.accPrj";
            if (saveFileDialog.ShowDialog() == true)
            {
                bool isTemplate = false;
                if (saveFileDialog.FilterIndex == 0)
                    isTemplate = true;

                var projectNodes = GetNodesAsProjectNodes(isTemplate);

                var filePath = System.IO.Path.GetDirectoryName(saveFileDialog.FileName);

                var project = new ProjectSettings()
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(saveFileDialog.FileName),
                    LastSaveDirectory = filePath,
                    Nodes = projectNodes
                };

                project.WriteToBinaryFile(saveFileDialog.FileName);

                this.Title = "ACCUSCHEDULE - " + project.Name;
            }
        }

        private List<ProjectNode> GetNodesAsProjectNodes(bool isTemplate = false)
        {
            var nodes = ViewModel.Network.Nodes.Items.ToList().Cast<DefaultNodeViewModel>().ToList();

            // Get all the nodes as serializable project nodes
            var allNodes = new List<ProjectNode>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes.ElementAt(i);

                var nodeTool = node as INodeProperties;
                var isSavable = true;
                if (nodeTool != null) 
                    isSavable = Tools.IsToolPayloadSavable(nodeTool.Tool);
                

                    // Get node Connections
                    var nodeConnections = new HashSet<DefaultNodeViewModel>();
                    foreach (var nodeInput in node.Inputs.Items)
                        foreach (var connection in nodeInput.Connections.Items)
                            nodeConnections.Add(connection.Output.Parent as DefaultNodeViewModel);

                    var filename = string.Empty;
                    if (node.TypeOfNode == DefaultNodeViewModel.NodeType.DataTable)
                    {
                        var startNode = node as DataTableStartNode;
                        filename = startNode.FileName;
                    }


                    var pNode = new ProjectNode()
                    {
                        ID = i,
                        Position = node.Position,
                        Name = node.Name,
                        FileName = filename,
                        Category = GetNodeCategory(node),
                        ToolClass = GetNodeToolClass(node),
                        ToolName = GetNodeToolname(node),
                        NodeType = node.TypeOfNode.ToString(),
                        XMLPayLoad = !isTemplate 
                                        ? isSavable 
                                            ? GetNodePayload(node) 
                                            : string.Empty 
                                        : string.Empty,
                        _Node = node,
                        _NodeConnections = nodeConnections,
                        Props = GetNodeProps(node)
                    };

                    allNodes.Add(pNode);
                
            }

            // Assign each nodes connections for serialization
            foreach (var node in allNodes)
            {
                var nodeConnections = new List<int>();
                foreach (var nodeConnection in node._NodeConnections)
                {
                    var connectedTo = allNodes.Where(w => w._Node == nodeConnection).FirstOrDefault();
                    if (connectedTo != null)
                        nodeConnections.Add(connectedTo.ID);
                }
                node.Connections = nodeConnections;
            }

            return allNodes;
        }

        private string GetNodePayload(dynamic Node)
        {
            var ret = string.Empty;

            if (Node.ValueEditor?.Value?.GetType() == typeof(DataTable))
            {
                var sw = new StringWriter();
                var pay = Node.ValueEditor.Value as DataTable;
                pay.WriteXml(sw, XmlWriteMode.WriteSchema);
                return sw.ToString();
            } else if (Node.ValueEditor?.Value?.GetType() == typeof(DataSet))
            {
                var sw = new StringWriter();
                var pay = Node.ValueEditor.Value as DataSet;
                pay.WriteXml(sw, XmlWriteMode.WriteSchema);
                return sw.ToString();
            }

                return ret;
        }

        private Type GetNodePayloadType(dynamic Node)
        {

            if (Node?.ValueEditor?.Value?.GetType() == typeof(DataTable)) return typeof(DataTable);
            else if (Node?.ValueEditor?.Value?.GetType() == typeof(DataSet)) return typeof(DataSet);
            
            return null;
        }

        private Dictionary<string, ParamTab> GetNodeProps(dynamic Node)
        {
            try
            {
                var props = Node.Properties as Dictionary<string, ParamTab>;
                return props;
            }
            catch
            {
                return null;
            }
            
        }
        private string GetNodeCategory(dynamic Node)
        {
            try
            {
                var cat = Node.Tool.Category as string;
                return cat;
            }
            catch
            {
                return string.Empty;
            }

        }
        private string GetNodeToolname(dynamic Node)
        {
            try
            {
                var name = Node.Tool.ToolMethodInfo.Name as string;
                return name;
            }
            catch
            {
                return string.Empty;
            }

        }
        private Type GetNodeToolClass(dynamic Node)
        {
            try
            {
                var t = Node.Tool.ToolMethodInfo.DeclaringType as Type;
                return t;
            }
            catch
            {
                return null;
            }

        }



        public void Load()
        {
            isLoading = true;
            var file = FileExtensions.OpenFileOrNull("Open Project File", "accTmplt", "Template file (*.accTmplt)|*.accTmplt|Project file (*.accPrj)|*.accPrj");

            if (file == null)
            {
                isLoading = false;
                return;
            }

            Properties.Settings.Default.LastSaveDir = System.IO.Path.GetDirectoryName(file.FileName);
            Properties.Settings.Default.Save();

            ProjectName = file.SafeFileName;
            this.Title = "ACCUSCHEDULE - " + ProjectName;

            var isTemplate = false;
            isTemplate = System.IO.Path.GetExtension(file.FileName) == ".accTmplt";

            var project = SaveLoadExtensions.ReadFromBinaryFile<ProjectSettings>(file.FileName);
            var nodes = LoadProjectNodes(project, isTemplate);

            HookUpConnections(nodes);

            if (!isTemplatePrj)
            {
                PopulateLoadedNodes(nodes);
                ShowLoadedNodes(nodes);
            } 
                

            isLoading = false;
            isTemplatePrj = false;
        }

        public void ShowLoadedNodes(IEnumerable<(DefaultNodeViewModel node, ProjectNode projNode)> nodes)
        {
            foreach (var nodeSet in nodes)
            {
                if (nodeSet.node.Name != "ReconcileSchedule" && nodeSet.node.Name != "GenerateXML" && nodeSet.node.Name != "ToTemplate" && nodeSet.node.Name != "BuildPaperWork")
                {
                    var nodeType = GetNodePayloadType(nodeSet.node);
                    if (nodeType == typeof(DataTable))
                    {
                        var tableFromXML = GetTableFromXML(nodeSet.projNode);
                        if (tableFromXML != null)
                        {
                            // Create new VT and populate
                            var newVT = new ViewTabs();
                            newVT.Header = nodeSet.projNode.Name;
                            newVT.Table = tableFromXML;
                            tables.UpdateTab(newVT);
                        }
                    }
                    else if (nodeType == typeof(DataSet))
                    {
                        var setFromXML = GetSetFromXML(nodeSet.projNode);
                        if (setFromXML != null && setFromXML.Tables.Count > 0)
                        {
                            // Create new VT and populate
                            var newVT = new ViewTabs();
                            newVT.Header = nodeSet.projNode.Name;
                            newVT.Set = setFromXML;
                            tables.UpdateTab(newVT);
                        }
                    }
                }
            }
        }
        public void PopulateLoadedNodes(IEnumerable<(DefaultNodeViewModel node, ProjectNode projNode)> nodes)
        {

                foreach (var nodeSet in nodes)
                {
                    if (nodeSet.node.Name != "ReconcileSchedule" && nodeSet.node.Name != "ToTemplate" && nodeSet.node.Name != "BuildPaperWork")
                    {
                        var nodeType = GetXMLPayloadType(nodeSet.projNode);
                        if (nodeType == typeof(DataTable))
                        {
                            var tableFromXML = GetTableFromXML(nodeSet.projNode);
                            if (tableFromXML != null)
                                SetNodeValueEditor(nodeSet.node, tableFromXML);
                        }
                        else if (nodeType == typeof(DataSet))
                        {
                            var setFromXML = GetSetFromXML(nodeSet.projNode);
                            if (setFromXML != null && setFromXML.Tables.Count > 0)
                                SetNodeValueEditor(nodeSet.node, setFromXML);
                        }
                    }
                }
            
        }

        public void SetNodeValueEditor(DefaultNodeViewModel nodeVM, object value)
        {
            dynamic node = nodeVM;

            if (value.GetType() == typeof(DataSet))
            {
                try
                {
                    node.ValueEditor.Value = value as DataSet ;
                }
                catch
                {
                }
            } 
            else if (value.GetType() == typeof(DataTable))
            {
                try
                {
                    node.ValueEditor.Value = value as DataTable;
                }
                catch
                {
                }
            }

            
            
        }

        public void HookUpConnections(IEnumerable<(DefaultNodeViewModel node, ProjectNode projNode)> nodes)
        {
            // Loop through each node set and find the node it's connected too.
            if (nodes == null) return;

            foreach (var nodeSet in nodes)
            {
                var nodeInput = GetNodeInput(nodeSet.node);
                nodeSet.node.IsSelected = true;


                if (nodeSet.node.Properties != null && nodeSet.node.Properties.Any() && isTemplatePrj == true
                        && nodeSet.node.Name != "ReconcileSchedule" && nodeSet.node.Name != "GenerateXML" && nodeSet.node.Name != "ToTemplate")
                {
                    // Get the submit button
                    
                    if (props.stack.Children.Count > 0)
                    {
                        var btnStack = props.stack.Children[0] as StackPanel;
                        if (btnStack != null)
                        {
                            var btn = btnStack.Children.OfType<Button>().FirstOrDefault();
                            if (btn != null)
                            {
                                btn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                do
                                {
                                    Thread.Sleep(10);
                                } while (isProcessing);
                            }
                        }
                    }

                }



                // Add connection for each node
                foreach (var connectedNodeID in nodeSet.projNode.Connections)
                {
                    var connectedNode = nodes.Where(w => w.projNode.ID == connectedNodeID).FirstOrDefault();
                    var connectedNodeOutput = GetNodeOutput(connectedNode.node);
                    

                    // Export Node: Make sure correct Output connection is selected.
                    if (connectedNodeOutput == null) // Means "Input" wasn't found... which means it's an export node.
                    {
                        // Check value type of node, either table or dataset
                        var payloadType = GetNodePayloadType(nodeSet.node);
                        if (payloadType == typeof(DataTable))
                            connectedNodeOutput = GetExportNodeOutputDataTable(connectedNode.node);
                        else if (payloadType == typeof(DataSet))
                        {
                            connectedNodeOutput = GetExportNodeOutputDataSet(connectedNode.node);
                        }
                    }

                    if (nodeSet.node.Name != "ReconcileSchedule" && nodeSet.node.Name != "GenerateXML" && nodeSet.node.Name != "ToTemplate" && nodeSet.node.Name != "BuildPaperWork")
                    {
                        var newConnection = new DefaultConnectionViewModel(ViewModel.Network, nodeInput, connectedNodeOutput);
                        ViewModel.Network.Connections.Add(newConnection);
                        do
                        {
                            Thread.Sleep(10);
                        } while (!GetNodeInputHasValue(connectedNode.node));
                        
                    }
                    
                    //ViewModel.Network.ConnectionFactory.Invoke(GetNodeInput(connectedNode.node), GetNodeOutput(nodeSet.node));
                }

                if (nodeSet.node.Name != "ReconcileSchedule" && nodeSet.node.Name != "GenerateXML" && nodeSet.node.Name != "ToTemplate")
                {
                    nodeSet.node.IsSelected = false;
                }
            }
        }

        private object GetNodeEditor(dynamic Node)
        {
            try
            {
                return Node?.ValueEditor?.Value;
            }
            catch
            {
                return null;
            }

        }
        private bool GetNodeInputHasValue(dynamic Node)
        {
            try
            {
                var t = Node.Inputs.Count;
                if (t != null)
                {
                    if (Convert.ToInt32(t) > 0) 
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }

        }
        private NodeInputViewModel GetNodeInput(dynamic Node)
        {
            try
            {
                var t = Node.Input as NodeInputViewModel;
                return t;
            }
            catch
            {
                 return null;
            }

        }
        private NodeOutputViewModel GetNodeOutput(dynamic Node)
        {
            try
            {
                var t = Node.Output as NodeOutputViewModel;
                return t;
            }
            catch
            {
                return null;
            }

        }
        private NodeOutputViewModel GetExportNodeOutputDataTable(dynamic Node)
        {
            try
            {
                var t = Node.dtConnectionFlow as NodeOutputViewModel;
                return t;
            }
            catch
            {
                return null;
            }

        }
        private NodeOutputViewModel GetExportNodeOutputDataSet(dynamic Node)
        {
            try
            {
                var t = Node.dsConnectionFlow as NodeOutputViewModel;
                return t;
            }
            catch
            {
                return null;
            }

        }

        public IEnumerable<(DefaultNodeViewModel node, ProjectNode projNode)> LoadProjectNodes(ProjectSettings project, bool isTemplate)
        {

            if (project == null) return null;

            // Clear the network
            ViewModel.Network.Nodes.Clear();

            var loadedNodes = new List<(DefaultNodeViewModel node, ProjectNode projNode)>();

            isTemplatePrj = string.IsNullOrEmpty(project.Nodes.Where(w => w.NodeType == "DataTable").FirstOrDefault().XMLPayLoad);
            isTemplatePrj = isTemplate;

            // Insert the nodes
            foreach (var node in project.Nodes)
            {
                var nodeObjToLoad = new ToolProcessor(this).GetToolOfNode(node.ToolName, node.ToolClass);

                // Get the node type
                _ = Enum.TryParse(node.NodeType, out DefaultNodeViewModel.NodeType nodeTypeEnum);
                if (nodeTypeEnum == DefaultNodeViewModel.NodeType.DataTable)
                {
                    var newTable = GetTableFromXML(node);
                    if (isTemplatePrj) newTable = null;
                    
                    // Template Project
                    if (newTable == null)
                    {
                        // Retrive table from filename
                        ExcelInputHandler ei = new ExcelInputHandler();

                        var file = node.FileName;
                        var tables = ei.ExtractTables(file);

                        // Find Table in tables matching node.Name
                        var matchingTable = tables.Where(w => w.Name == node.Name).FirstOrDefault();
                        if (matchingTable != null)
                        {
                            // Load the table into the start node
                            var newNode = new DataTableStartNode(matchingTable.AsNativeDataTable(), node.FileName);
                            newNode.Position = node.Position;
                            ViewModel.Network.Nodes.Add(newNode);
                            loadedNodes.Add((newNode, node));
                        }
                        else return null;
                    }
                    else
                    { // Project
                        var newNode = new DataTableStartNode(new DataTable(), node.FileName);
                        newNode.Position = node.Position;
                        ViewModel.Network.Nodes.Add(newNode);
                        loadedNodes.Add((newNode, node));
                    }

                    
                } else if (nodeTypeEnum == DefaultNodeViewModel.NodeType.DataSetFromTable)
                {
                    var newNode = new DataSetFromTableProcessingNode(node.Category, this);
                    newNode.Properties = node.Props;
                    newNode.Name = node.Name;
                    newNode.Position = node.Position;
                    newNode.Tool = nodeObjToLoad.Tool;
                    newNode.InjectedObjects = nodeObjToLoad.Injections;
                    ViewModel.Network.Nodes.Add(newNode);
                    loadedNodes.Add((newNode, node));
                }
                else if (nodeTypeEnum == DefaultNodeViewModel.NodeType.DataSet)
                {
                    var newNode = new DataSetProcessingNode(node.Category, this);
                    newNode.Properties = node.Props;
                    newNode.Name = node.Name;
                    newNode.Position = node.Position;
                    newNode.Tool = nodeObjToLoad.Tool;
                    newNode.InjectedObjects = nodeObjToLoad.Injections;
                    ViewModel.Network.Nodes.Add(newNode);
                    loadedNodes.Add((newNode, node));
                }
                else if (nodeTypeEnum == DefaultNodeViewModel.NodeType.DataTableFromSet)
                {
                    var newNode = new DataTableFromSetProcessingNode(node.Category, this);
                    newNode.Properties = node.Props;
                    newNode.Name = node.Name;
                    newNode.Position = node.Position;
                    newNode.Tool = nodeObjToLoad.Tool;
                    newNode.InjectedObjects = nodeObjToLoad.Injections;
                    ViewModel.Network.Nodes.Add(newNode);
                    loadedNodes.Add((newNode, node));
                }
                else if (nodeTypeEnum == DefaultNodeViewModel.NodeType.TableProcessing)
                {
                    var newNode = new DataTableProcessingNode(this, node.Category);
                    newNode.Properties = node.Props;
                    newNode.Name = node.Name;
                    newNode.Position = node.Position;
                    newNode.Tool = nodeObjToLoad.Tool;
                    newNode.InjectedObjects = nodeObjToLoad.Injections;
                    ViewModel.Network.Nodes.Add(newNode);
                    loadedNodes.Add((newNode, node));
                }
                else if (nodeTypeEnum == DefaultNodeViewModel.NodeType.Void)
                {
                    var newNode = new ExportNode(node.Category);
                    newNode.Properties = node.Props;
                    newNode.Name = node.Name;
                    newNode.Position = node.Position;
                    newNode.Tool = nodeObjToLoad.Tool;
                    newNode.InjectedObjects = nodeObjToLoad.Injections;
                    ViewModel.Network.Nodes.Add(newNode);
                    loadedNodes.Add((newNode, node));
                }
                else if (nodeTypeEnum == DefaultNodeViewModel.NodeType.ObjList)
                {
                    var newNode = new ObjListToDataSetNode(node.Category, this);
                    newNode.Properties = node.Props;
                    newNode.Name = node.Name;
                    newNode.Position = node.Position;
                    newNode.Tool = nodeObjToLoad.Tool;
                    newNode.InjectedObjects = nodeObjToLoad.Injections;
                    ViewModel.Network.Nodes.Add(newNode);
                    loadedNodes.Add((newNode, node));
                }


            }
            return loadedNodes;
        }

        private static DataTable GetTableFromXML(ProjectNode node)
        {
            if (string.IsNullOrEmpty(node.XMLPayLoad)) return null;
            var newTable = new DataTable();
            newTable.ReadXml(new StringReader(node.XMLPayLoad));
            return newTable;
        }
        private static DataSet GetSetFromXML(ProjectNode node)
        {
            var newSet = new DataSet();
            newSet.ReadXml(new StringReader(node.XMLPayLoad));
            return newSet;
        }
        private static Type GetXMLPayloadType(ProjectNode node)
        {
            try
            {
                var tableTest = new DataTable();
                tableTest.ReadXml(new StringReader(node.XMLPayLoad));
                if (tableTest.Columns.Count == 0 && tableTest.Rows.Count == 0)
                {
                    var setTest = new DataSet();
                    setTest.ReadXml(new StringReader(node.XMLPayLoad));

                    if (setTest.Tables.Count == 0)
                        return null;
                    else
                        return typeof(DataSet);
                } 
                else
                    return typeof(DataTable);
            }
            catch
            {

            }

            return null;
            
        }


        private void SetDisplayNodeChain()
        {
            if (ShowNodeChain.IsChecked == false)
                ShowNodeChain.IsChecked = true;
            else if (ShowNodeChain.IsChecked == true)
                ShowNodeChain.IsChecked = false;
        }
        private void SetLightTheme()
        {

            throw new NotImplementedException();
            /*
            PaletteHelper paletteHelper = new PaletteHelper();
            ITheme theme = paletteHelper.GetTheme();
            
            if (DarkTheme.IsChecked == true)
            {
                ToggleBaseColour(true);

                //ThemeManager.Current.ChangeTheme(this, "Dark.Red");
            }
            else if (DarkTheme.IsChecked == false)
            {
                ToggleBaseColour(false);
                //ThemeManager.Current.ChangeTheme(this, "Dark.Red");
            }
            paletteHelper.SetTheme(theme);
            */
        }

        private void ToggleBaseColour(bool isDark)
        {
            var ld = isDark ? "BaseDark" : "BaseLight";
            var t = System.Windows.Application.Current.Resources.MergedDictionaries.ElementAt(3);
            Uri uri = new Uri($"pack://application:,,,/MahApps.Metro;component/Styles/Accents/" + ld + ".xaml");
            System.Windows.Application.Current.Resources.MergedDictionaries.RemoveAt(3);
            System.Windows.Application.Current.Resources.MergedDictionaries.Insert(3, new ResourceDictionary() { Source = uri });

            PaletteHelper _paletteHelper = new PaletteHelper();
            ITheme theme = _paletteHelper.GetTheme();
            IBaseTheme baseTheme = isDark ? new MaterialDesignDarkTheme() : (IBaseTheme)new MaterialDesignLightTheme();
            theme.SetBaseTheme(baseTheme);
            _paletteHelper.SetTheme(theme);
        }


        public void AddTestCategories()
        {
            List<ISetting> appSettings = ((App)Application.Current).SettingsContainer;

            if (appSettings.Any()) return;

            var controlPair = new StackPanel() { HorizontalAlignment = HorizontalAlignment.Stretch, Orientation = Orientation.Horizontal };
            controlPair.Children.Add(new Label() { Content = "TestLabel", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(5,5,5,0) });
            controlPair.Children.Add(new TextBox() { Text = "Testing This Out", HorizontalAlignment = HorizontalAlignment.Stretch });

            var controlPair2 = new StackPanel() { HorizontalAlignment = HorizontalAlignment.Stretch, Orientation = Orientation.Horizontal };
            controlPair2.Children.Add(new Label() { Content = "TestLabel", HorizontalAlignment = HorizontalAlignment.Left });
            controlPair2.Children.Add(new TextBox() { Text = "Testing This Out", HorizontalAlignment = HorizontalAlignment.Stretch });

            // Add a couple TEST settings to the RH menu
            TestSettingInterface ts = new TestSettingInterface("TestLabel"
                , controlPair
                , controlPair2);

            TestSettingInterface ts2 = new TestSettingInterface("TestLabel2"
                , new TextBlock() { Text = "TestLabel2" }
                , new TextBox() { Text = "Testing This Out2" });
            appSettings.Add(ts);
            appSettings.Add(ts2);
        }

        private void lblExport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            
        }

        public void AddToDataSet(DataTable datatable, string File)
        {
            this.tables.AddNodeToList(datatable, this.ViewModel, File);
        }

       


        private void PropsItem_LostPropFocus(UIElement element)
        {
            // Get the selected node and write the settings back
            if (LastNodeSelected != null)
            {
                var txtBox = element as TextBox;
                if (txtBox != null)
                {
                    var pt = txtBox.Tag as ParamTab;
                    pt.Value = txtBox.Text;

                    if (LastNodeSelected.Properties
                    .Any(a => a.Key.Equals(txtBox.Name)))
                        LastNodeSelected.Properties[txtBox.Name] = pt; // Found, modify existing value
                    else
                        LastNodeSelected.Properties.Add(txtBox.Name, pt); // Not Found, add new value
                }

                var cmboBox = element as ComboBox;
                if (cmboBox != null)
                {
                    var pt2 = cmboBox.Tag as ParamTab;
                    pt2.Value = cmboBox.Text;

                    if (LastNodeSelected.Properties
                    .Any(a => a.Key.Equals(cmboBox.Name)))
                        LastNodeSelected.Properties[cmboBox.Name] = pt2; // Found, modify existing value
                    else
                        LastNodeSelected.Properties.Add(cmboBox.Name, pt2); // Not Found, add new value
                }
            }
        }

        private void ViewModel_OnScopeUpdated(ViewModels.ViewTabs item)
        {
            UpdateAutoLock();

            scope.UpdateScope(item);

            props.UpdateProperties(Tools.ProcessMethodInputParameters(item), item.Props);

            if (ShowNodeChain.IsChecked)
            {
                // Loop through the connected nodes and refresh the table view
                foreach (var node in item.NodeChain)
                    UpdateTables(node);
            }

            LastNodeSelected = this.ViewModel.Network.SelectedNodes.Items.FirstOrDefault() as DefaultNodeViewModel;
        }
        private void UpdateTables(KeyValuePair<DefaultNodeViewModel, IEnumerable<object>> nodeAndObjects)
        {
            tables.tabs.Items.Clear();

            if (nodeAndObjects.Key != null && nodeAndObjects.Value.Any())
            {
                foreach (var obj in nodeAndObjects.Value)
                {
                    var vt = new ViewTabs();

                    var dt = obj as DataTable;
                    if (dt != null) vt.Table = dt;

                    var ds = obj as DataSet;
                    if (ds != null) vt.Set = ds;

                    if (dt != null || ds != null)
                        tables.UpdateTab(vt);
                }

                
                
            }
        }
        private void ViewModel_OnTableRemoved(ViewModels.ViewTabs item)
        {
            tables.RemoveTab(item.Table);
        }
        private void ViewModel_OnTableUpdated(ViewModels.ViewTabs item)
        {
            tables.UpdateTab(item);
        }
        private void ViewModel_OnTableAdded(ViewModels.ViewTabs item)
        {
            tables.AddTab(item);
        }

        private void LockAutoHide_MouseUp(object sender, MouseButtonEventArgs e) => UpdateAutoLock(true);
        private void LockAutoHide_Click(object sender, RoutedEventArgs e) => UpdateAutoLock(true);
        private void UpdateAutoLock(bool UpdateLastAutoProp = false)
        {
            /*
            if (!isLastColWidthForPropAuto)
            {
                PropWidthLock.Width = new GridLength(lastColWidthForProps);
                if (UpdateLastAutoProp)
                {
                    isLastColWidthForPropAuto = true;
                    lblAutoHide.Inlines.Clear();
                    lblAutoHide.Inlines.Add(new Run("Lock Width"));
                }
            }
            else
            {
                PropWidthLock.Width = GridLength.Auto;
                if (UpdateLastAutoProp)
                {
                    isLastColWidthForPropAuto = false;
                    lblAutoHide.Inlines.Clear();
                    lblAutoHide.Inlines.Add(new Run("Auto-Size"));
                }
            }
            */
        }
        private void RightSplitter_DragCompleted(object sender, DragCompletedEventArgs e) 
        { 
            lastColWidthForProps = PropWidthLock.ActualWidth;
            Properties.Settings.Default.LastPropWinWidth = PropWidthLock.Width.Value;
            Properties.Settings.Default.Save();
        }

        private void UpperSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (Properties.Settings.Default.LastTableWinHeight > 0)
            {
                Properties.Settings.Default.LastTableWinHeight = UpperSplitRow.Height.Value;
                Properties.Settings.Default.Save();
            }
        }
        private void PropHeightSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (Properties.Settings.Default.LastPropWinHeight > 0)
            {
                Properties.Settings.Default.LastPropWinHeight = PropWinHeight.Height.Value;
                Properties.Settings.Default.Save();
            }
        }
        private void LeftSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (Properties.Settings.Default.LastNodeListWidth > 0)
            {
                Properties.Settings.Default.LastNodeListWidth = LeftSplitter.Width.Value;
                Properties.Settings.Default.Save();
            }
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastWindowState = (int)this.WindowState;
            Properties.Settings.Default.Save();
        }

        private void MetroWindow_LocationChanged(object sender, EventArgs e)
        {
            if (initialWindowLocate)
            {
                Properties.Settings.Default.LastWindowPosition_Left = this.Left;
                Properties.Settings.Default.LastWindowPosition_Top = this.Top;
                Properties.Settings.Default.Save();
            } else
            {
                WindowState savedWinState = (WindowState)Enum.ToObject(typeof(WindowState), Properties.Settings.Default.LastWindowState);
                this.WindowState = savedWinState;
            }

            initialWindowLocate = true;
        }
    }
}
