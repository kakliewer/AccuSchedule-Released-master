using NodeNetwork.Toolkit.ValueNode;
using NodeNetwork.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    [Serializable]
    public class DefaultInputViewModel<T> : ValueNodeInputViewModel<T>
    {
        static DefaultInputViewModel() => Splat.Locator.CurrentMutable.Register(() => new NodeInputView(), typeof(IViewFor<DefaultInputViewModel<T>>));

        public DefaultInputViewModel(PortType type)
        {
            this.Port = new DefaultPortViewModel { TypeOfPort = type };

            //if (type == PortType.Execution)
            //{
                this.PortPosition = NodeNetwork.ViewModels.PortPosition.Right;
            //}
        }
    }
}
