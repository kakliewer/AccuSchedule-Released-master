using AccuSchedule.UI.Views.Dialogs;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Web.UI;
using AccuSchedule.UI.Extensions;
using ClosedXML.Report;

namespace AccuSchedule.UI.ViewModels
{
    public class ExportResultsDialogViewModel : INotifyPropertyChanged
    {

        private bool _hasErrors;
        public bool HasErrors
        {
            get { return _hasErrors; }
            set
            {
                this.MutateVerbose(ref _hasErrors, value, RaisePropertyChanged());
            }
        }

        private TemplateErrors _errors;
        public TemplateErrors Errors
        {
            get { return _errors; }
            set
            {
                this.MutateVerbose(ref _errors, value, RaisePropertyChanged());
            }
        }





        public event PropertyChangedEventHandler PropertyChanged;
        private Action<PropertyChangedEventArgs> RaisePropertyChanged()
        {
            return args => PropertyChanged?.Invoke(this, args);
        }

        

    }
}
