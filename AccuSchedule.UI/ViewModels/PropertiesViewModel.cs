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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    [DataContract]
    public class PropertiesViewModel : ReactiveObject
    {
        #region Code
        [DataMember]
        public DataTableValue Code
        {
            get => _code;
            set => this.RaiseAndSetIfChanged(ref _code, value);
        }
        [IgnoreDataMember]
        private DataTableValue _code;
        #endregion

        [DataMember]
        public DataTable Table
        {
            get => _table;
            set => this.RaiseAndSetIfChanged(ref _table, value);
        }
        [IgnoreDataMember]
        private DataTable _table = new DataTable();


        public PropertiesViewModel()
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
