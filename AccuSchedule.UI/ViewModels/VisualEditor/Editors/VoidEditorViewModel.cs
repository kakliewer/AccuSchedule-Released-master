using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
using AccuSchedule.UI.Views.VisualEditor.Editors;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Editors
{
    [Serializable]
    public class VoidEditorViewModel : ValueEditorViewModel<IEnumerable<object>>
    {
        static VoidEditorViewModel() => Splat.Locator.CurrentMutable.Register(() => new VoidValueEditorView(), typeof(IViewFor<VoidEditorViewModel>));

        public VoidEditorViewModel(IEnumerable<object> items)
        {
            Value = items;
        }
    }
}
