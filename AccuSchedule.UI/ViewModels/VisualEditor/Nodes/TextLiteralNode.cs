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
    public class TextLiteralNode : DefaultNodeViewModel
    {
        static TextLiteralNode() => Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<TextLiteralNode>));

        public StringValueEditorViewModel ValueEditor { get; } = new StringValueEditorViewModel();
        public ValueNodeOutputViewModel<ITypedExpression<string>> Output { get; }

        public TextLiteralNode() : base(NodeType.Literal)
        {
            this.Name = "Text";

            Output = new DefaultOutputViewModel<ITypedExpression<string>>(PortType.String)
            {
                Name = "Value",
                Editor = ValueEditor,
                Value = ValueEditor.ValueChanged.Select(v => new StringLiteral { Value = v })
            };
            this.Outputs.Add(Output);
        }
    }
}
