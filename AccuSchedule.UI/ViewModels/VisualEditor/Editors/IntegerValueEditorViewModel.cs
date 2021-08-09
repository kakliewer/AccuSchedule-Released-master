using AccuSchedule.UI.Views.VisualEditor.Editors;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Editors
{
    [Serializable]
    public class IntegerValueEditorViewModel : ValueEditorViewModel<int?>
    {
        static IntegerValueEditorViewModel()
        {
            Splat.Locator.CurrentMutable.Register(() => new IntegerValueEditorView(), typeof(IViewFor<IntegerValueEditorViewModel>));
        }

        public IntegerValueEditorViewModel()
        {
            Value = 0;
        }
    }
}
