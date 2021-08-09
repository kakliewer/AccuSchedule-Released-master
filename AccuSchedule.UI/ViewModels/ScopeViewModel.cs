using AccuSchedule.UI.Models;
using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    public class ScopeViewModel : ReactiveObject
    {
        #region Code
        public DataTableValue Code
        {
            get => _code;
            set => this.RaiseAndSetIfChanged(ref _code, value);
        }
        private DataTableValue _code;
        #endregion
        public DataTable Table
        {
            get => _table;
            set => this.RaiseAndSetIfChanged(ref _table, value);
        }
        private DataTable _table = new DataTable();


        public ScopeViewModel()
        {

            this.WhenAnyValue(vm => vm.Table).Where(c => c != null)
                .Select(c =>
                {
                    // Do something to pre-process
                    return c;
                })
                .ToProperty(this, vm => vm.Table, _table);

        }

    }
}
