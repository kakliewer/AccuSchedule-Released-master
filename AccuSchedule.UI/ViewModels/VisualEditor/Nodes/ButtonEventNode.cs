using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.Views;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    public class ButtonEventNode : DefaultNodeViewModel
    {
        static ButtonEventNode() => Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<ButtonEventNode>));

        public ValueListNodeInputViewModel<IStatement> OnClickFlow { get; }

        public ButtonEventNode() : base(NodeType.EventNode)
        {
            this.Name = "Button Events";

            OnClickFlow = new DefaultListInputViewModel<IStatement>(PortType.Execution) { Name = "On Click" };

            this.Inputs.Add(OnClickFlow);
        }
    }
}
