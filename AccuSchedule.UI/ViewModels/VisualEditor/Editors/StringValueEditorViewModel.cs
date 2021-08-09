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
    public class StringValueEditorViewModel : ValueEditorViewModel<string>
    {
        static StringValueEditorViewModel() => Splat.Locator.CurrentMutable.Register(() => new StringValueEditorView(), typeof(IViewFor<StringValueEditorViewModel>));

        public StringValueEditorViewModel()
        {
            Value = "";
        }
    }
}
