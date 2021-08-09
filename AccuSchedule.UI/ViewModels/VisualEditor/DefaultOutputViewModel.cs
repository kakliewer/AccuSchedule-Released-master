using NodeNetwork.Toolkit.ValueNode;
using NodeNetwork.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodeNetwork.ViewModels;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;
using System.Runtime.Serialization;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    [DataContract]
    public class DefaultOutputViewModel<T> : ValueNodeOutputViewModel<T>
    {
        static DefaultOutputViewModel() => Splat.Locator.CurrentMutable.Register(() => new NodeOutputView(), typeof(IViewFor<DefaultOutputViewModel<T>>));

        public DefaultOutputViewModel(PortType type)
        {
            this.Port = new DefaultPortViewModel { TypeOfPort = type };

            //if (type == PortType.Execution)
            //{
                this.PortPosition = PortPosition.Left;
            //}
        }
    }
}
