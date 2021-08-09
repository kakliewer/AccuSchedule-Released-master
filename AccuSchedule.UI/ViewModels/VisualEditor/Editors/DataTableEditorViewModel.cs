using AccuSchedule.UI.Views.VisualEditor.Editors;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Editors
{
    [DataContract]
    public class DataTableEditorViewModel : ValueEditorViewModel<DataTable>
    {
        static DataTableEditorViewModel() => Splat.Locator.CurrentMutable.Register(() => new DataTableValueEditorView(), typeof(IViewFor<DataTableEditorViewModel>));

        public DataTableEditorViewModel(DataTable table)
        {
            Value = table;
        }
    }
}
