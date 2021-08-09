using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    [Serializable]
    public class DataTableStartNode : DefaultNodeViewModel
    {

        static DataTableStartNode() => Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<DataTableStartNode>));


        DataTableEditorViewModel _valueEditor { get; set; } = new DataTableEditorViewModel(null);
        public DataTableEditorViewModel ValueEditor => _valueEditor;

        public DefaultListInputViewModel<DataTableValue> Input { get; }
        public DefaultOutputViewModel<DataTableValue> Output { get; }

        public string FileName { get; set; }

        public DataTableStartNode(DataTable table, string fileName) : base(NodeType.DataTable)
        {
            if (table == null) return;
            this.Name = table?.TableName;
            this.Category = "Imported";
            this._valueEditor = new DataTableEditorViewModel(table);
            this.FileName = fileName;

            Input = new DefaultListInputViewModel<DataTableValue>(PortType.DataTable)
            {
                Name = string.Format("Rows: {0}, Cols: {1}", table.Rows.Count, table.Columns.Count)
            };
            this.Inputs.Add(Input);


            Output = new DefaultOutputViewModel<DataTableValue>(PortType.DataTable)
            {
                Name = string.Format("{0}", table.TableName),
                Editor = ValueEditor,
                Value = ValueEditor.ValueChanged.Select(v => new DataTableValue { Value = v })
            };

            ValueEditor?.ValueChanged.Subscribe(s =>
            {
                Input.Name = string.Format("Rows: {0}, Cols: {1}", ValueEditor.Value.Rows.Count, ValueEditor.Value.Columns.Count);
                this.Name = ValueEditor.Value?.TableName;
            });

            // Add the value to the connector
            var obsVal = Observable.Return(new DataTableValue() { Value = table });
            this.Output.Value = obsVal;
        }
    }
}
