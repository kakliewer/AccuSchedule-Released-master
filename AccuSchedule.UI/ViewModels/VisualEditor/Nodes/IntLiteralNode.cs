using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
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
    public class IntLiteralNode : DefaultNodeViewModel
    {
        static IntLiteralNode() => Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<IntLiteralNode>));

        public IntegerValueEditorViewModel ValueEditor { get; } = new IntegerValueEditorViewModel();

        public ValueNodeOutputViewModel<ITypedExpression<int>> Output { get; }

        public IntLiteralNode() : base(NodeType.Literal)
        {
            this.Name = "Integer";

            Output = new DefaultOutputViewModel<ITypedExpression<int>>(PortType.Integer)
            {
                Editor = ValueEditor,
                Value = ValueEditor.ValueChanged.Select(v => new IntLiteral { Value = v ?? 0 })
            };
            this.Outputs.Add(Output);
        }
    }
}
