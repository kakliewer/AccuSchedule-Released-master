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
    public class DefaultPendingConnectionViewModel : PendingConnectionViewModel
    {
        static DefaultPendingConnectionViewModel() => Splat.Locator.CurrentMutable.Register(() => new DefaultPendingConnectionView(), typeof(IViewFor<DefaultPendingConnectionViewModel>));

        public DefaultPendingConnectionViewModel(NetworkViewModel parent) : base(parent)
        {

        }
    }
}
