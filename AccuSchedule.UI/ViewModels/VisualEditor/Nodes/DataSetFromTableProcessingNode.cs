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
    public class DataSetFromTableProcessingNode : DefaultNodeViewModel, INodeProperties
    {
        // Boiler Plate
        static DataSetFromTableProcessingNode()
        {
            Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<DataSetFromTableProcessingNode>));
        }

        // Setup Validators
        DataSetEditorViewModel _valueEditor { get; set; } = new DataSetEditorViewModel(null);
        public DataSetEditorViewModel ValueEditor => _valueEditor;
        public DefaultListInputViewModel<DataSetValue> Input { get; }
        public DefaultOutputViewModel<DataTableValue> Output { get; }

        // INodeProperties
        public ToolID Tool { get; set; }
        public List<MemberInfo> InjectedObjects { get; set; }

        private MainWindow Host { get; set; }

        public DataSetFromTableProcessingNode(string category, MainWindow host) : base(NodeType.DataSetFromTable)
        {
            // Header of Node
            this.Name = "BaseSetFromTable";
            this.Category = category;
            Host = host;

            // Setup Input reactive hook loading resources.
            Input = new DefaultListInputViewModel<DataSetValue>(PortType.DataSet)
            {
                Name = "DataSet"
            };
            this.Inputs.Add(Input); // Add the port!

            // Setup Output reactive hook loading resources.
            Output = new DefaultOutputViewModel<DataTableValue>(PortType.DataTable)
            {
                Name = string.Format("{0}", ""),
                Editor = ValueEditor,
                Value = ValueEditor?.ValueChanged?.Select(v => new DataTableValue { Value = v?.Tables[v.Tables.Count - 1] })
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

            ValueEditor.ValueChanged.Subscribe(s =>
            {
                if (this.ValueEditor != null)
                    this.Output.Name = string.Format("Tables: {0}", this.ValueEditor?.Value?.Tables?.Count);
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
            else if (input?.Parent?.GetType() == typeof(DataTableProcessingNode))
            {
                var connectionInputVM = input.Parent as DataTableProcessingNode;
                connectionValue = new DataTableValue() { Value = connectionInputVM?.ValueEditor?.Value };
            }


            if (connectionValue != null)
            {
                this.Input.Name = "DataSet";

                // Do some processing to the data table before placng it in this Output
                if (Host.isLoading)
                {
                    if (Host.isTemplatePrj)
                        ProcessNode(connectionValue?.Value?.Copy());
                } 
                else ProcessNode(connectionValue?.Value?.Copy());

                this.Output.Name = string.Format("Tables: {0}", this.ValueEditor?.Value?.Tables?.Count);

                // Add the value to the connector
                var obsVal = Observable.Return(new DataTableValue() { Value = connectionValue?.Value?.Copy() });
                this.Output.Value = obsVal;
            }
        }

        void ProcessNode(DataTable table)
        {
            // DO SOMETHING
            if (table == null) return;

            // Execute the Tool Method
            var tp = new ToolProcessor(Host);

            var result = tp.Executioner(Tool, table) as DataSet;

            // Set the new dataset as the output value
            if (this.ValueEditor == null) this._valueEditor = new DataSetEditorViewModel(result);
            this.ValueEditor.Value = result;
        }

    }
}
