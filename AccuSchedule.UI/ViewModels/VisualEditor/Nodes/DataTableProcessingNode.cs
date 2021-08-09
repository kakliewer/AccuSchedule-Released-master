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
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    [Serializable]
    public class DataTableProcessingNode : DefaultNodeViewModel, INodeProperties
    {
        // Boiler Plate
        static DataTableProcessingNode()
        {
            Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<DataTableProcessingNode>));
        }


        // Setup Validators
        DataTableEditorViewModel _valueEditor { get; set; } = new DataTableEditorViewModel(null);
        public DataTableEditorViewModel ValueEditor => _valueEditor;
        public DefaultListInputViewModel<DataTableValue> Input { get; }
        public DefaultOutputViewModel<DataTableValue> Output { get; }

        // INodeProperties
        public ToolID Tool { get; set; }
        public List<MemberInfo> InjectedObjects { get; set; }

        private MainWindow Host { get; set; }
        public DataTableProcessingNode(MainWindow host, string category = "") : base(NodeType.TableProcessing)
        {
            // Header of Node
            this.Name = "BaseTable";
            this.Category = category;
            Host = host;

            // Setup Input reactive hook loading resources.
            Input = new DefaultListInputViewModel<DataTableValue>(PortType.DataTable)
            {
                Name = "DataTable"
            };
            this.Inputs.Add(Input); // Add the port!

            // Setup Output reactive hook loading resources.
            Output = new DefaultOutputViewModel<DataTableValue>(PortType.DataTable)
            {
                Name = string.Format("{0}", ""),
                Editor = ValueEditor,
                Value = ValueEditor?.ValueChanged?.Select(v => new DataTableValue { Value = v })
            };
            this.Outputs.Add(Output); // Add the port!

            // Monitor incoming connections
            Output.Connections.CountChanged.Subscribe(index =>
            {
                // Check if DataTableStartNode
                var connection = index > 0 ? Output.Connections.Items.ElementAt(index - 1) : null;
                var connectionInput = connection?.Input as DefaultListInputViewModel<DataTableValue>;
                HandleNode(connectionInput);

            });

            ValueEditor?.ValueChanged.Subscribe(s =>
            {
                this.Output.Name = string.Format("Rows: {0}, Cols: {1}", this.ValueEditor?.Value?.Rows.Count, this.ValueEditor?.Value?.Columns.Count);
            });

        }

        void HandleNode(DefaultListInputViewModel<DataTableValue> input)
        {
            DataTableValue connectionValue = null;
            if (input?.Parent?.GetType() == typeof(DataTableStartNode))
            {
                var connectionInputVM = input.Parent as DataTableStartNode;
                connectionValue = new DataTableValue() { Value = connectionInputVM.ValueEditor.Value };
            }
            else if(input?.Parent?.GetType() == typeof(DataTableProcessingNode))
            {
                var connectionInputVM = input.Parent as DataTableProcessingNode;
                connectionValue = new DataTableValue() { Value = connectionInputVM?.ValueEditor?.Value };
            }


            if (connectionValue != null)
            {
                this.Input.Name = "DataTable";
                if (this.ValueEditor == null) this._valueEditor = new DataTableEditorViewModel(connectionValue?.Value?.Copy());



                // Do some processing to the data table before placng it in this Output
                if (Host.isLoading)
                {
                    if (Host.isTemplatePrj)
                    {
                        this.ValueEditor.Value = connectionValue?.Value?.Copy();
                        ProcessNode(connectionValue?.Value?.Copy());
                    }

                }
                else
                {
                    this.ValueEditor.Value = connectionValue?.Value?.Copy();
                    ProcessNode(connectionValue?.Value?.Copy());
                }

                this.Output.Name = string.Format("Rows: {0}, Cols: {1}", this.ValueEditor?.Value?.Rows.Count, this.ValueEditor?.Value?.Columns.Count);

                // Add the value to the connector
                var obsVal = Observable.Return(new DataTableValue() { Value = connectionValue?.Value?.Copy() });
                this.Output.Value = obsVal;
            }
        }

        void ProcessNode(DataTable table)
        {
            if (table == null) return;

            var tp = new ToolProcessor(Host);
            // If no action parameters are found then process the data, otherwise pass through the value
            if (!Tool.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(Action<DataSet>)))
            {
                var result = tp.Executioner(Tool, table) as DataTable;

                // Set the new dataset as the output value
                if (this.ValueEditor == null) this._valueEditor = new DataTableEditorViewModel(result);
                this.ValueEditor.Value = result;
            }

        }

    }
}
