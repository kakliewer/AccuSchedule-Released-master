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
    public class DataSetEditorViewModel : ValueEditorViewModel<DataSet>
    {
        static DataSetEditorViewModel() => Splat.Locator.CurrentMutable.Register(() => new DataSetValueEditorView(), typeof(IViewFor<DataSetEditorViewModel>));

        public DataSetEditorViewModel(DataSet set)
        {
            Value = set;
        }
    }
}
