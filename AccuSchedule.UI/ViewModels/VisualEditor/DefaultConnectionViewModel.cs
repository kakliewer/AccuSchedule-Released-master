using AccuSchedule.UI.Views.VisualEditor;
using NodeNetwork.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    [Serializable]
    public class DefaultConnectionViewModel : ConnectionViewModel
    {
        static DefaultConnectionViewModel() => Splat.Locator.CurrentMutable.Register(() => new DefaultConnectionView(), typeof(IViewFor<DefaultConnectionViewModel>));

        public DefaultConnectionViewModel(NetworkViewModel parent, NodeInputViewModel input, NodeOutputViewModel output) : base(parent, input, output)
        {

        }
    }
}
