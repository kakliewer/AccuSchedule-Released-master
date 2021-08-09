using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models;
using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    [DataContract]
    public class DataTableFromSetProcessingNode : DefaultNodeViewModel, INodeProperties
    {
        // Boiler Plate
        static DataTableFromSetProcessingNode()
        {
            Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<DataTableFromSetProcessingNode>));
        }


        // Setup Validators
        [DataMember]
        DataTableEditorViewModel _valueEditor { get; set; } = new DataTableEditorViewModel(null);
        [IgnoreDataMember]
        public DataTableEditorViewModel ValueEditor => _valueEditor;

        public DefaultListInputViewModel<DataTableValue> Input { get; }
        public DefaultOutputViewModel<DataSetValue> Output { get; }

        // INodeProperties
        [DataMember]
        public ToolID Tool { get; set; }
        [IgnoreDataMember]
        public List<MemberInfo> InjectedObjects { get; set; }

        private MainWindow Host { get; set; }


        public DataTableFromSetProcessingNode(string category, MainWindow host) : base(NodeType.DataTableFromSet)
        {
            // Header of Node
            this.Name = "BaseTableFromSet";
            this.Category = category;
            Host = host;

            // Setup Input reactive hook loading resources.
            Input = new DefaultListInputViewModel<DataTableValue>(PortType.DataTable)
            {
                Name = "DataTable"
            };
            this.Inputs.Add(Input); // Add the port!

            // Setup Output reactive hook loading resources.
            Output = new DefaultOutputViewModel<DataSetValue>(PortType.DataSet)
            {
                Name = string.Format("{0}", ""),
                Editor = ValueEditor,
                Value = ValueEditor?.ValueChanged?.Where(w => w != null).Select(v => {
                    var ds = new DataSet();
                    ds.Tables.Add(v);
                    return new DataSetValue { Value = ds };
                })
            };
            this.Outputs.Add(Output); // Add the port!

            // Monitor incoming connections
            Output.Connections.CountChanged.Subscribe(index =>
            {
                // Check if DataTableStartNode
                var connection = index > 0 ? Output.Connections.Items.ElementAt(index - 1) : null;
                HandleNode(connection);

            });

            ValueEditor?.ValueChanged.Subscribe(s =>
            {
                this.Output.Name = string.Format("Rows: {0}, Cols: {1}", this.ValueEditor?.Value?.Rows.Count, this.ValueEditor?.Value?.Columns.Count);
            });

        }

        void HandleNode(ConnectionViewModel connectionVM)
        {
            if (connectionVM == null) return;

            DataSetValue connectionValue = null;

            var inputDS = connectionVM?.Input as DefaultListInputViewModel<DataSetValue>;

            if (inputDS?.Parent?.GetType() == typeof(DataSetProcessingNode))
            {
                var connectionInputVM = inputDS.Parent as DataSetProcessingNode;
                connectionValue = new DataSetValue() { Value = connectionInputVM.ValueEditor.Value };
            } else if (inputDS?.Parent?.GetType() == typeof(DataSetFromTableProcessingNode))
            {
                var connectionInputVM = inputDS.Parent as DataSetFromTableProcessingNode;
                connectionValue = new DataSetValue() { Value = connectionInputVM.ValueEditor.Value };
            }


            if (connectionValue != null)
            {
                this.Input.Name = "DataTable";
                if (Host.isLoading)
                {
                    if (Host.isTemplatePrj)
                        ProcessDataSetInputNode(connectionValue?.Value?.Copy());
                }
                else ProcessDataSetInputNode(connectionValue?.Value?.Copy());
                
                this.Output.Name = string.Format("Rows: {0}, Cols: {1}", this.ValueEditor?.Value?.Rows.Count, this.ValueEditor?.Value?.Columns.Count);

                // Add the value to the connector
                var obsVal = Observable.Return(new DataSetValue() { Value = connectionValue?.Value?.Copy() });
                this.Output.Value = obsVal;
            }
        }

        void ProcessDataSetInputNode(DataSet set)
        {
            // DO SOMETHING
            if (set == null) return;

            var tp = new ToolProcessor(Host);
            var result = tp.Executioner(Tool, set) as DataTable;

            // Set the new dataset as the output value
            if (this.ValueEditor == null) this._valueEditor = new DataTableEditorViewModel(result);
            this.ValueEditor.Value = result;

        }

    }
}
