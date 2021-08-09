using NodeNetwork.Toolkit.ValueNode;
using NodeNetwork.ViewModels;
using NodeNetwork.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    [DataContract]
    public class DefaultListInputViewModel<T> : ValueListNodeInputViewModel<T>
    {
        static DefaultListInputViewModel() => Splat.Locator.CurrentMutable.Register(() => new NodeInputView(), typeof(IViewFor<DefaultListInputViewModel<T>>));


        public DefaultListInputViewModel(PortType type)
        {
            this.Port = new DefaultPortViewModel { TypeOfPort = type };

            this.PortPosition = PortPosition.Right;
        }

    }
}
