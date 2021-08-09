using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    public class PrintNode : DefaultNodeViewModel
    {
        static PrintNode()
        {
            Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<PrintNode>));
        }

        public ValueNodeInputViewModel<ITypedExpression<string>> Text { get; }

        public ValueNodeOutputViewModel<DataTableValue> Flow { get; }

        public PrintNode() : base(NodeType.Function)
        {
            this.Name = "Print";

            Text = new DefaultInputViewModel<ITypedExpression<string>>(PortType.String)
            {
                Name = "Text"
            };
            this.Inputs.Add(Text);

            Flow = new DefaultOutputViewModel<DataTableValue>(PortType.Execution)
            {
                Name = "",
                Value = this.Text.ValueChanged.Select(stringExpr => new FunctionCall
                {
                    FunctionName = "print",
                    Parameters =
                    {
                        stringExpr ?? new StringLiteral{Value = ""}
                    }
                })
            };
            this.Outputs.Add(Flow);
        }
    }
}
