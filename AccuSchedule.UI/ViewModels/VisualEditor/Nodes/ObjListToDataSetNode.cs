using AccuSchedule.UI.Extensions;
using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models;
using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
using NodeNetwork.Toolkit;
using NodeNetwork.Toolkit.ValueNode;
using NodeNetwork.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    [Serializable]
    public class ObjListToDataSetNode : DefaultNodeViewModel, INodeProperties
    {
        // Boiler Plate
        static ObjListToDataSetNode()
        {
            Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<ObjListToDataSetNode>));
        }

        // Setup Validators
        DataSetEditorViewModel _valueEditor { get; set; } = new DataSetEditorViewModel(null);
        public DataSetEditorViewModel ValueEditor => _valueEditor;
        public DefaultListInputViewModel<DataSetValue> Input { get; }
        public DefaultOutputViewModel<DataSetValue> Output { get; }

        // INodeProperties
        public ToolID Tool { get; set; }
        public List<MemberInfo> InjectedObjects { get; set; }
        private MainWindow Host { get; set; }

        public List<object> ObjectList { get; set; }



        public ObjListToDataSetNode(string category, MainWindow host) : base(NodeType.ObjList)
        {
            // Header of Node
            this.Name = "BaseObjList";
            this.Category = category;
            Host = host;

            // Setup Input reactive hook loading resources.
            Input = new DefaultListInputViewModel<DataSetValue>(PortType.DataSet)
            {
                Name = "DataSet"
            };
            this.Inputs.Add(Input); 


            // Allow connections from table or set
            Output = new DefaultOutputViewModel<DataSetValue>(PortType.DataSet)
            {
                Name = string.Format("")
            };
            this.Outputs.Add(Output);


            // Monitor incoming connections
            Output.Connections.CountChanged.Subscribe(index =>
            {
                // Check if DataTableStartNode
                var connection = index > 0 ? Output.Connections.Items.ElementAt(index - 1) : null;
                var connectionInput = connection?.Input as DefaultListInputViewModel<DataSetValue>;
                ExecuteAgainstConnection(connectionInput);

            });

            ValueEditor?.ValueChanged.Subscribe(s =>
            {
                    this.Output.Name = string.Format("Tables: {0}", this.ValueEditor?.Value?.Tables?.Count);
            });


        }

        public void ExecuteAgainstConnection(DefaultListInputViewModel<DataSetValue> input)
        {
            // Get all attached processing nodes
            var otherConnections = GraphAlgorithms.GetConnectedNodesBubbling(this, false, true, false).Cast<DefaultNodeViewModel>();

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

            ObjectList = new List<object>(objList);

            if (Host.isLoading)
            {
                if (Host.isTemplatePrj)
                {
                    ProcessNode(objList);
                }
            }
            else
            {
                ProcessNode(objList);
            }
            
        }

        void ProcessNode(IEnumerable<object> objList)
        {
            if (!objList.Any()) return;

            var tp = new ToolProcessor(Host);
            // If no action parameters are found then process the data, otherwise pass through the value
            if (!Tool.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(Action<IEnumerable<object>>)))
            {
                var result = tp.ExecutionerObjList(Tool, objList) as DataSet;

                // Set the new dataset as the output value
                if (this.ValueEditor == null) this._valueEditor = new DataSetEditorViewModel(result);
                this.ValueEditor.Value = result;
            }

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
                case DefaultNodeViewModel.NodeType.Void:
                    break;
                case DefaultNodeViewModel.NodeType.ObjList:
                    var ods = node as DataTableFromSetProcessingNode;
                    return ods.ValueEditor?.Value;
                default:
                    break;
            }


            return ret;
        }



    }
}
