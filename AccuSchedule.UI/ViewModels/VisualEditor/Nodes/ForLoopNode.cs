using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    public class ForLoopNode : DefaultNodeViewModel
    {
        static ForLoopNode() => Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<ForLoopNode>));

        public ValueNodeOutputViewModel<IStatement> FlowIn { get; }

        public ValueListNodeInputViewModel<IStatement> LoopBodyFlow { get; }
        public ValueListNodeInputViewModel<IStatement> LoopEndFlow { get; }

        public ValueNodeInputViewModel<ITypedExpression<int>> FirstIndex { get; }
        public ValueNodeInputViewModel<ITypedExpression<int>> LastIndex { get; }

        public ValueNodeOutputViewModel<ITypedExpression<int>> CurrentIndex { get; }

        public ForLoopNode() : base(NodeType.FlowControl)
        {
            this.Name = "For Loop";

            LoopBodyFlow = new DefaultListInputViewModel<IStatement>(PortType.Execution)
            {
                Name = "Loop Body"
            };
            this.Inputs.Add(LoopBodyFlow);

            LoopEndFlow = new DefaultListInputViewModel<IStatement>(PortType.Execution)
            {
                Name = "Loop End"
            };
            this.Inputs.Add(LoopEndFlow);

            FirstIndex = new DefaultInputViewModel<ITypedExpression<int>>(PortType.Integer)
            {
                Name = "First Index"
            };
            this.Inputs.Add(FirstIndex);

            LastIndex = new DefaultInputViewModel<ITypedExpression<int>>(PortType.Integer)
            {
                Name = "Last Index"
            };
            this.Inputs.Add(LastIndex);

            ForLoop value = new ForLoop();

            var loopBodyChanged = LoopBodyFlow.Values.Connect().Select(_ => Unit.Default).StartWith(Unit.Default);
            var loopEndChanged = LoopEndFlow.Values.Connect().Select(_ => Unit.Default).StartWith(Unit.Default);
            FlowIn = new DefaultOutputViewModel<IStatement>(PortType.Execution)
            {
                Name = "",
                Value = Observable.CombineLatest(loopBodyChanged, loopEndChanged, FirstIndex.ValueChanged, LastIndex.ValueChanged,
                        (bodyChange, endChange, firstI, lastI) => (BodyChange: bodyChange, EndChange: endChange, FirstI: firstI, LastI: lastI))
                    .Select(v => {
                        value.LoopBody = new StatementSequence(LoopBodyFlow.Values.Items);
                        value.LoopEnd = new StatementSequence(LoopEndFlow.Values.Items);
                        value.LowerBound = v.FirstI ?? new IntLiteral { Value = 0 };
                        value.UpperBound = v.LastI ?? new IntLiteral { Value = 1 };
                        return value;
                    })
            };
            this.Outputs.Add(FlowIn);

            CurrentIndex = new DefaultOutputViewModel<ITypedExpression<int>>(PortType.Integer)
            {
                Name = "Current Index",
                Value = Observable.Return(new VariableReference<int> { LocalVariable = value.CurrentIndex })
            };
            this.Outputs.Add(CurrentIndex);
        }
    }
}
