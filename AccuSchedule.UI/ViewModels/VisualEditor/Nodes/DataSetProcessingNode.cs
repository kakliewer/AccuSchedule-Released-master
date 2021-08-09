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
    public class DataSetProcessingNode : DefaultNodeViewModel, INodeProperties
    {
        // Boiler Plate
        static DataSetProcessingNode()
        {
            Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<DataSetProcessingNode>));
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




        public DataSetProcessingNode(string category, MainWindow host) : base(NodeType.DataSet)
        {
            // Header of Node
            this.Name = "BaseSet";
            this.Category = category;
            Host = host;

            // Setup Input reactive hook loading resources.
            Input = new DefaultListInputViewModel<DataSetValue>(PortType.DataSet)
            {
                Name = "DataSet"
            };
            this.Inputs.Add(Input); // Add the port!

            // Setup Output reactive hook loading resources.
            Output = new DefaultOutputViewModel<DataSetValue>(PortType.DataSet)
            {
                Name = string.Format("{0}", ""),
                Editor = ValueEditor,
                Value = ValueEditor?.ValueChanged?.Select(v => new DataSetValue { Value = v })
            };
            this.Outputs.Add(Output); // Add the port!

            // Monitor incoming connections
            Output.Connections.CountChanged.Subscribe(index =>
            {
                // Check if DataTableStartNode
                var connection = index > 0 ? Output.Connections.Items.ElementAt(index - 1) : null;
                var connectionInput = connection?.Input as DefaultListInputViewModel<DataSetValue>;
                HandleNode(connectionInput);

            });

            ValueEditor?.ValueChanged.Subscribe(s =>
            {
                this.Output.Name = string.Format("Tables: {0}", this.ValueEditor?.Value?.Tables?.Count);
            });

        }


        void HandleNode(DefaultListInputViewModel<DataSetValue> input)
        {
            DataSetValue connectionValue = null;
            if (input?.Parent?.GetType() == typeof(DataSetProcessingNode))
            {
                var connectionInputVM = input.Parent as DataSetProcessingNode;
                connectionValue = new DataSetValue() { Value = connectionInputVM?.ValueEditor?.Value };
            }
            else if (input?.Parent?.GetType() == typeof(DataSetFromTableProcessingNode))
            {
                var connectionInputVM = input.Parent as DataSetFromTableProcessingNode;
                connectionValue = new DataSetValue() { Value = connectionInputVM?.ValueEditor?.Value };
            }


            if (connectionValue != null)
            {
                this.Input.Name = "DataSet";
                if (this.ValueEditor == null) this._valueEditor = new DataSetEditorViewModel(connectionValue?.Value?.Copy());

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

                this.Output.Name = string.Format("Tables: {0}", this.ValueEditor?.Value?.Tables?.Count);

                // Add the value to the connector
                var obsVal = Observable.Return(new DataSetValue() { Value = connectionValue?.Value?.Copy() });
                this.Output.Value = obsVal;
            }
        }

        void ProcessNode(DataSet set)
        {
            if (set == null) return;

            var tp = new ToolProcessor(Host);
            // If no action parameters are found then process the data, otherwise pass through the value
            if (!Tool.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(Action<DataSet>)))
            { 
                var result = tp.Executioner(Tool, set) as DataSet;

                // Set the new dataset as the output value
                if (this.ValueEditor == null) this._valueEditor = new DataSetEditorViewModel(result);
                this.ValueEditor.Value = result;
            }

        }

    }
}
