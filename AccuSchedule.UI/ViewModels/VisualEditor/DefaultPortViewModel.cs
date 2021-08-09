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
    public class DefaultPortViewModel : PortViewModel
    {

        public enum PortType
        {
            Execution, Integer, String, DataTable, DataTableProcessing, DataSet
        }

        static DefaultPortViewModel() => Splat.Locator.CurrentMutable.Register(() => new Views.VisualEditor.DefaultPortView(), typeof(IViewFor<DefaultPortViewModel>));

        public PortType TypeOfPort
        {
            get => _portType;
            set => this.RaiseAndSetIfChanged(ref _portType, value);
        }
        private PortType _portType;

    }
}
