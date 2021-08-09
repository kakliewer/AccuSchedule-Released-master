using AccuSchedule.UI.Extensions;
using AccuSchedule.UI.Models;
using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
using AccuSchedule.UI.Views.VisualEditor;
using DocumentFormat.OpenXml.Wordprocessing;
using DynamicData;
using NodeNetwork;
using NodeNetwork.Toolkit;
using NodeNetwork.Toolkit.Layout.ForceDirected;
using NodeNetwork.Toolkit.NodeList;
using NodeNetwork.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    public class MainViewModel : ReactiveObject
    {

        public NetworkViewModel Network { get; } = new NetworkViewModel();

        public DefaultNodeListViewModel NodeList { get; } = new DefaultNodeListViewModel();
        
        public TablesViewModel Tables { get; } = new TablesViewModel();

        

        public ScopeViewModel Scope { get; } = new ScopeViewModel();
        
        public PropertiesViewModel PropView { get; } = new PropertiesViewModel();

        public static readonly RoutedCommand ShowNodeChainCommand = new RoutedUICommand("ShowNodeChain", "ShowNodeChainCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.Q, ModifierKeys.Control)
        }));

        public ReactiveCommand<Unit, Unit> AutoLayout { get; }
        public ReactiveCommand<Unit, Unit> StartAutoLayoutLive { get; }
        public ReactiveCommand<Unit, Unit> StopAutoLayoutLive { get; }

        public ReactiveCommand<Unit, Unit> InsertTable { get; }

        public delegate void TableEvent(ViewTabs item);
        public event TableEvent OnTableAdded;
        public event TableEvent OnTableUpdated;
        public event TableEvent OnTableRemoved;
        public event TableEvent OnScopeUpdated;

        public delegate void VoidEvent(IDictionary<ExportNode, IEnumerable<object>>VoidNodeAndResults);
        public event VoidEvent OnVoidUpdated;
        public delegate void ObListUpdatedEvent(IDictionary<DataSetProcessingNode, IEnumerable<object>> ObjList);
        public event ObListUpdatedEvent OnObjListUpdated;

        public int nodeCount { get; set; }

        public MainViewModel()
        {
            // Update the DataTable views when there is any change in the network
            Network.Connections.CountChanged.Subscribe(cnt => RefreshTableView());


            // Removal of start nodes.
            Network.SelectedNodes.Connect().Subscribe(items => {
                DefaultNodeViewModel lastNode = null;
                foreach (var nodeItem in items)
                {
                    var node = nodeItem.Item.Current as DefaultNodeViewModel;

                    if (node != null)
                    {
                        if (node.IsSelected && nodeItem.Reason == ListChangeReason.Remove)
                            RemoveView(node);

                        if (node.IsSelected) lastNode = node;
                    }
                }
                

                // Display the last selected node
                if (lastNode != null)
                {
                    DisplayScope(lastNode);
                }
            });

            
            // When any nodes are changed in the network
            Network.Nodes.Connect().Subscribe(items => {
                foreach (var nodeItem in items)
                {
                    var node = nodeItem.Item.Current as DefaultNodeViewModel;

                    if (node != null)
                    {
                        
                        // Check if node is TableStartNode and add to top view
                        switch (node.TypeOfNode)
                        {
                            case DefaultNodeViewModel.NodeType.TableProcessing:
                                break;
                            case DefaultNodeViewModel.NodeType.DataTable:
                                if (nodeItem.Reason == ListChangeReason.Add) 
                                    HandleNewDataTableNode(node);
                                break;
                            case DefaultNodeViewModel.NodeType.Void:
                                if (nodeItem.Reason == ListChangeReason.Add)
                                    HandleNewVoidNode(node);
                                break;
                            default:
                                break;
                        }
                    }
                }

                
            });



            //Network.Connections.CountChanged.Subscribe(index => { RefreshTableView(); });

            //NodeList.AddNodeType(() => new ButtonEventNode());   //<--Example
            //NodeList.AddNodeType(() => new ForLoopNode());
            //NodeList.AddNodeType(() => new IntLiteralNode());
            //NodeList.AddNodeType(() => new PrintNode());
            //NodeList.AddNodeType(() => new TextLiteralNode());
            //NodeList.AddNodeType(() => new DataTableProcessingNode());
            //NodeList.AddNodeType(() => new DataTableFromSetProcessingNode());
            //NodeList.AddNodeType(() => new DataSetProcessingNode());
            //NodeList.AddNodeType(() => new DataSetFromTableProcessingNode());

            ForceDirectedLayouter layouter = new ForceDirectedLayouter();
            var config = new Configuration
            {
                Network = Network,
            };
            AutoLayout = ReactiveCommand.Create(() => layouter.Layout(config, 10000));
            StartAutoLayoutLive = ReactiveCommand.CreateFromObservable(() =>
                Observable.StartAsync(ct => layouter.LayoutAsync(config, ct)).TakeUntil(StopAutoLayoutLive)
            );
            StopAutoLayoutLive = ReactiveCommand.Create(() => { }, StartAutoLayoutLive.IsExecuting);
            
        }


        private void DisplayScope(DefaultNodeViewModel node)
        {

            if (node.TypeOfNode == DefaultNodeViewModel.NodeType.DataTable
                || node.TypeOfNode == DefaultNodeViewModel.NodeType.TableProcessing
                || node.TypeOfNode == DefaultNodeViewModel.NodeType.DataTableFromSet
                || node.TypeOfNode == DefaultNodeViewModel.NodeType.DataSet
                || node.TypeOfNode == DefaultNodeViewModel.NodeType.DataSetFromTable
                || node.TypeOfNode == DefaultNodeViewModel.NodeType.Void
                || node.TypeOfNode == DefaultNodeViewModel.NodeType.ObjList)
            {
                var tabItem = new ViewTabs();
                tabItem.Node = node;
                tabItem.NodeChain = GetConnectedObjects(node);
                if (node.GetType() == typeof(DataTableStartNode))
                {
                    var dtNode = node as DataTableStartNode;
                    tabItem.Header = dtNode.Output.CurrentValue.Value.TableName;
                    tabItem.getTable = true; // Set the value table by the return function
                    tabItem.Table = dtNode.Output.CurrentValue.Value;
                    tabItem.ToolMethod = null;
                    tabItem.Props = dtNode.Properties;
                }
                else if (node.GetType() == typeof(DataTableProcessingNode))
                {
                    var dtNode = node as DataTableProcessingNode;
                    tabItem.Header = dtNode.ValueEditor?.Value?.TableName;
                    tabItem.getTable = true; // Set the value table by the return function
                    tabItem.Table = dtNode.ValueEditor?.Value;
                    tabItem.ToolMethod = dtNode.Tool;
                    tabItem.Props = dtNode.Properties;
                    tabItem.InjectedObjects = dtNode.InjectedObjects;

                }
                else if (node.GetType() == typeof(DataTableFromSetProcessingNode))
                {
                    var dtNode = node as DataTableFromSetProcessingNode;
                    tabItem.Header = dtNode.ValueEditor?.Value?.TableName;

                    // Set the dataset input parameter since we're returning a different data type
                    var dsvVM = dtNode.Outputs.Items.FirstOrDefault() as DefaultOutputViewModel<DataSetValue>;
                    var dsvConnections = dsvVM?.Connections.Items.FirstOrDefault();
                    var dsvParentVM = dsvConnections?.Input as DefaultListInputViewModel<DataSetValue>;

                    var dsvParentDST = dsvParentVM?.Parent as DataSetFromTableProcessingNode;
                    if (dsvParentDST != null) tabItem.Set = dsvParentDST.ValueEditor?.Value;

                    var dsvParentDSP = dsvParentVM?.Parent as DataSetProcessingNode;
                    if (dsvParentDSP != null) tabItem.Set = dsvParentDSP.ValueEditor?.Value;

                    tabItem.getTable = true; // Set the value table by the return function

                    tabItem.ToolMethod = dtNode.Tool;
                    tabItem.Props = dtNode.Properties;
                    tabItem.InjectedObjects = dtNode.InjectedObjects;
                }
                else if (node.GetType() == typeof(DataSetProcessingNode))
                {
                    var dtNode = node as DataSetProcessingNode;
                    tabItem.Header = dtNode.ValueEditor?.Value?.DataSetName;
                    tabItem.getSet = true;
                    tabItem.Set = dtNode.ValueEditor?.Value;
                    tabItem.ToolMethod = dtNode.Tool;
                    tabItem.Props = dtNode.Properties;
                    tabItem.InjectedObjects = dtNode.InjectedObjects;
                }
                else if (node.GetType() == typeof(DataSetFromTableProcessingNode))
                {
                    var dtNode = node as DataSetFromTableProcessingNode;
                    tabItem.getTable = false; 
                    tabItem.getSet = true;
                    tabItem.Header = dtNode.ValueEditor?.Value?.DataSetName;
                    tabItem.Set = dtNode.ValueEditor?.Value;
                    tabItem.ToolMethod = dtNode.Tool;
                    tabItem.Props = dtNode.Properties;
                    tabItem.InjectedObjects = dtNode.InjectedObjects;
                }
                else if (node.GetType() == typeof(ExportNode))
                {
                    var exNode = node as ExportNode;
                    tabItem.getTable = false;
                    tabItem.getSet = false;
                    tabItem.isVoid = true;
                    tabItem.ObjPayload = exNode.ValueEditor?.Value;
                    tabItem.ToolMethod = exNode.Tool;
                    tabItem.Props = exNode.Properties;
                    tabItem.InjectedObjects = exNode.InjectedObjects;
                }
                else if (node.GetType() == typeof(ObjListToDataSetNode))
                {
                    var objListNode = node as ObjListToDataSetNode;
                    tabItem.Header = objListNode.ValueEditor?.Value?.DataSetName;
                    tabItem.getObjects = true;
                    tabItem.Set = objListNode.ValueEditor?.Value;
                    tabItem.ObjPayload = objListNode.ObjectList;
                    tabItem.ToolMethod = objListNode.Tool;
                    tabItem.Props = objListNode.Properties;
                    tabItem.InjectedObjects = objListNode.InjectedObjects;
                }

                OnScopeUpdated(tabItem);

            }
            
        }

        public void RefreshTableView() => RefreshNodes();

        public void RefreshNodes()
        {
            if (GraphAlgorithms.FindLoops(Network).Any()) return;
            RefreshStartNodesViews();
            RefreshVoidNodes();
        }


        private Dictionary<DefaultNodeViewModel, IEnumerable<object>> GetConnectedObjects(DefaultNodeViewModel parentNode)
        {
            var ret = new Dictionary<DefaultNodeViewModel, IEnumerable<object>>();

            // Get all attached processing nodes
            var otherConnections = GraphAlgorithms.GetConnectedNodesBubbling(parentNode, false, true, true).Cast<DefaultNodeViewModel>();

            // Get a list of all the objects from each node
            var objList = new HashSet<object>();
            foreach (var node in otherConnections)
            {
                var nodeObj = GetResultFromNode(node);
                var ConnectionsWithObjectsOfSameType = GraphAlgorithms.GetConnectedNodesBubbling(node, true, false, false).Cast<DefaultNodeViewModel>()
                    .Where(w => otherConnections.Contains(w) && ToolsExtensions.IsSameObjectByName(nodeObj, GetResultFromNode(w)));

                if (nodeObj != null && !ConnectionsWithObjectsOfSameType.Any())
                    objList.Add(nodeObj);
            }

            ret[parentNode] = objList;


            return ret;
        }

        
        

        private void RefreshVoidNodes()
        {
            // Get all export nodes
            var exportNodes = Network.Nodes.Items
                .Where(node => node.GetType() == typeof(ExportNode));

            var exportObjects = new Dictionary<ExportNode, IEnumerable<object>>();

            // Loop through each start node
            foreach (ExportNode exportnode in exportNodes)
            {
                // Get all attached processing nodes
                var otherConnections = GraphAlgorithms.GetConnectedNodesBubbling(exportnode, false, true, false).Cast<DefaultNodeViewModel>();

                // Get a list of all the objects from each node
                var objList = new HashSet<object>();
                foreach (var node in otherConnections)
                {
                    var nodeObj = GetResultFromNode(node);
                    var ConnectionsWithObjectsOfSameType = GraphAlgorithms.GetConnectedNodesBubbling(node, true, false, false).Cast<DefaultNodeViewModel>()
                        .Where(w => otherConnections.Contains(w) && ToolsExtensions.IsSameObjectByName(nodeObj, GetResultFromNode(w)) );

                    if (nodeObj != null && !ConnectionsWithObjectsOfSameType.Any()) 
                        objList.Add(nodeObj);
                }

                exportObjects[exportnode] = objList;
            }

            if (exportObjects != null && exportObjects.Any())
                OnVoidUpdated(exportObjects);
        }

        public object GetResultFromNode(DefaultNodeViewModel node)
        {
            object ret = null;

            switch (node.TypeOfNode)
            {
                case DefaultNodeViewModel.NodeType.TableProcessing:
                    var tp = node as DataTableStartNode;
                    return tp.ValueEditor?.Value;
                case DefaultNodeViewModel.NodeType.DataTable:
                    var dt = node as DataTableStartNode;
                    return dt.ValueEditor?.Value;
                case DefaultNodeViewModel.NodeType.DataSet:
                    var ds = node as DataSetProcessingNode;
                    return ds.ValueEditor?.Value;
                case DefaultNodeViewModel.NodeType.DataSetFromTable:
                    var dst = node as DataSetFromTableProcessingNode;
                    return dst.ValueEditor?.Value;
                case DefaultNodeViewModel.NodeType.DataTableFromSet:
                    var dts = node as DataTableFromSetProcessingNode;
                    return dts.ValueEditor?.Value;
                case DefaultNodeViewModel.NodeType.ObjList:
                    var oln = node as ObjListToDataSetNode;
                    return oln.ValueEditor?.Value;
                case DefaultNodeViewModel.NodeType.Void:
                    break;
                default:
                    break;
            }


            return ret;
        }


        private void RefreshStartNodesViews()
        {
            // Get all start nodes
            var dtNodes = Network.Nodes.Items
                .Where(node => node.GetType() == typeof(DataTableStartNode));

            // Loop through each start node
            foreach (DataTableStartNode tableChain in dtNodes)
            {
                // Get all attached processing nodes
                var otherConnections = GraphAlgorithms.GetConnectedNodesTunneling(tableChain).Cast<DefaultNodeViewModel>();

                DefaultNodeViewModel lastNode = null;
                if (otherConnections.Any())
                {
                    var lastConnectedNode = otherConnections.LastOrDefault();
                    var firstConnectionSet = lastConnectedNode.Outputs.Items.FirstOrDefault() as DefaultOutputViewModel<DataTableValue>;
                    if (firstConnectionSet != null)
                    {
                        var parent = firstConnectionSet.Parent as DefaultNodeViewModel;
                        UpdateView(parent);
                    }
                    var firstConnectionTable = lastConnectedNode.Outputs.Items.FirstOrDefault() as DefaultOutputViewModel<DataTableValue>;
                    if (firstConnectionTable != null)
                    {
                        var parent = firstConnectionTable.Parent as DefaultNodeViewModel;
                        UpdateView(parent);
                    }

                }
                else
                    UpdateView(tableChain); // If just the table is left
            }
        }

        public void HandleNewDataSetNode(DefaultNodeViewModel node)
        {

            var dsNode = node as DataSetProcessingNode;
            if (dsNode != null)
            {
                //var dtOutput = exportNode.Output as DefaultOutputViewModel<DataTableValue>;
            }


        }

        public void HandleNewVoidNode(DefaultNodeViewModel node)
        {

            var exportNode = node as ExportNode;
            if (exportNode != null)
            {
                //var dtOutput = exportNode.Output as DefaultOutputViewModel<DataTableValue>;
            }


        }
        public void HandleNewDataTableNode(DefaultNodeViewModel node)
        {

            var dtNode = node as DataTableStartNode;
            if (dtNode != null)
            {
                var dtOutput = dtNode.Output as DefaultOutputViewModel<DataTableValue>;
                var tableEditor = dtOutput?.Editor as DataTableEditorViewModel;
                if (tableEditor != null)
                {
                    // Add a new tab representing the content.
                    var tabItem = new ViewTabs() { Header = tableEditor.Value.TableName, Table = tableEditor.Value, Props = dtNode.Properties };
                    OnTableAdded(tabItem);
                }
            }


        }


        public void UpdateView(DefaultNodeViewModel node)
        {
            if (node == null) return;

            var lastNode = node;
            
            switch (lastNode.TypeOfNode)
            {
                case DefaultNodeViewModel.NodeType.DataTable:
                    var sn = lastNode as DataTableStartNode;
                    OnTableUpdated(new ViewTabs() { Header = sn.ValueEditor?.Value?.TableName, Table = sn.ValueEditor?.Value, Props = sn.Properties });
                    break;
                case DefaultNodeViewModel.NodeType.TableProcessing:
                    var dt = lastNode as DataTableProcessingNode;
                    OnTableUpdated(new ViewTabs() { Header = dt.ValueEditor?.Value?.TableName, Table = dt.ValueEditor?.Value, ToolMethod = dt.Tool, Props = dt.Properties, InjectedObjects = dt.InjectedObjects });
                    break;
                case DefaultNodeViewModel.NodeType.DataSet:
                    var ds = lastNode as DataSetProcessingNode;
                    ProcessDataSet(ds.ValueEditor?.Value, ds.Tool, ds.Properties, ds.InjectedObjects);
                    break;
                case DefaultNodeViewModel.NodeType.DataSetFromTable:
                    var dst = lastNode as DataSetFromTableProcessingNode;
                    ProcessDataSet(dst.ValueEditor?.Value, dst.Tool, dst.Properties, dst.InjectedObjects);
                    break;
                case DefaultNodeViewModel.NodeType.DataTableFromSet:
                    var dts = lastNode as DataTableFromSetProcessingNode;
                    OnTableUpdated(new ViewTabs() { Header = dts.ValueEditor?.Value?.TableName, Table = dts.ValueEditor?.Value, ToolMethod = dts.Tool, Props = dts.Properties, InjectedObjects = dts.InjectedObjects });
                    break;
                case DefaultNodeViewModel.NodeType.ObjList:
                    var oln = lastNode as ObjListToDataSetNode;
                    ProcessDataSet(oln.ValueEditor?.Value, oln.Tool, oln.Properties, oln.InjectedObjects);
                    break;
                default:
                    OnTableUpdated(new ViewTabs() { Header = "", Table = null });
                    break;
            }
              
        }

        public void ProcessDataSet(DataSet set, ToolID tool, Dictionary<string, ParamTab> parameters, List<MemberInfo> ObjectsToInject)
        {
            if (set == null) return;

            foreach (DataTable table in set.Tables)
            {
                OnTableUpdated(new ViewTabs() { Header = table.TableName, Table = table, ToolMethod = tool, Props = parameters, InjectedObjects = ObjectsToInject });
            }
        }

        public void RemoveView(DefaultNodeViewModel node)
        {

            var dtNode = node as DataTableStartNode;
            if (dtNode != null)
            {
                node.IsSelected = false;

                var dtOutput = dtNode.Output as DefaultOutputViewModel<DataTableValue>;
                var tableEditor = dtOutput.Editor as DataTableEditorViewModel;
                if (tableEditor != null)
                {
                    // Add a new tab representing the content.
                    var tabItem = new ViewTabs() { Header = tableEditor.Value?.TableName, Table = tableEditor.Value };
                    OnTableRemoved(tabItem);
                    return;
                }
            }

            var dtsNode = node as DataTableFromSetProcessingNode;
            if (dtsNode != null)
            {
                node.IsSelected = false;
                var tableEditor = dtsNode.ValueEditor;
                if (tableEditor != null)
                {
                    // Add a new tab representing the content.
                    var tabItem = new ViewTabs() { Header = tableEditor.Value?.TableName, Table = tableEditor.Value };
                    OnTableRemoved(tabItem);
                    return;
                }
            }

            var dtpNode = node as DataTableProcessingNode;
            if (dtpNode != null)
            {
                node.IsSelected = false;
                var tableEditor = dtpNode.ValueEditor;
                if (tableEditor != null)
                {
                    // Add a new tab representing the content.
                    var tabItem = new ViewTabs() { Header = tableEditor.Value?.TableName, Table = tableEditor.Value };
                    OnTableRemoved(tabItem);
                    return;
                }
            }

            // Before Removing DataSets, Check if table exists in previously connected node first.
            var dspNode = node as DataSetProcessingNode;
            if (dspNode != null)
            {
                node.IsSelected = false;
                var tableEditor = dspNode.ValueEditor;
                if (tableEditor != null && tableEditor.Value != null && !SetExistInConnectedNode(dspNode, tableEditor?.Value?.DataSetName))
                {
                    foreach (DataTable table in tableEditor.Value?.Tables)
                    {
                        // Add a new tab representing the content.
                        var tabItem = new ViewTabs() { Header = table?.TableName, Table = table };
                        OnTableRemoved(tabItem);
                    }
                    return;
                }
            }

            var dstNode = node as DataSetFromTableProcessingNode;
            if (dstNode != null)
            {
                node.IsSelected = false;
                var tableEditor = dstNode.ValueEditor;
                if (tableEditor != null && tableEditor.Value != null && !SetExistInConnectedNode(node, tableEditor.Value?.DataSetName))
                {
                    foreach (DataTable table in tableEditor.Value?.Tables)
                    {
                        // Add a new tab representing the content.
                        var tabItem = new ViewTabs() { Header = table.TableName, Table = table };
                        OnTableRemoved(tabItem);
                    }
                    return;
                }
            }

            var olNode = node as ObjListToDataSetNode;
            if (olNode != null)
            {
                node.IsSelected = false;
                var tableEditor = olNode.ValueEditor;
                if (tableEditor != null && tableEditor.Value != null && !SetExistInConnectedNode(node, tableEditor.Value?.DataSetName))
                {
                    foreach (DataTable table in tableEditor.Value?.Tables)
                    {
                        // Add a new tab representing the content.
                        var tabItem = new ViewTabs() { Header = table.TableName, Table = table };
                        OnTableRemoved(tabItem);
                    }
                    return;
                }
            }

        }

        private bool SetExistInConnectedNode(DefaultNodeViewModel nodeVM, string SetName)
        {
            bool ret = false;

            //IEnumerable<DefaultOutputViewModel<DataSetValue>> dsNode = null;
            try
            {
                var dsNode = nodeVM.Outputs.Items.Cast<DefaultOutputViewModel<DataSetValue>>();

                foreach (var nodeConnection in dsNode)
                {
                    var connectionVal = nodeConnection.CurrentValue;
                    if (connectionVal?.Value?.DataSetName == SetName)
                        return true;
                }
            }
            catch (Exception)
            { // Cast failed meaning there is no more DataSets in the connected nodes
                return false; 
            }
            

            return ret;
        }


    }
}
