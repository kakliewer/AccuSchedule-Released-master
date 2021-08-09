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
using DocumentFormat.OpenXml.Office.CustomUI;

namespace AccuSchedule.UI.ViewModels
{
    public class WaitingDialogViewModel : INotifyPropertyChanged
    {


        private TemplateErrors _message;
        public TemplateErrors Message
        {
            get { return _message; }
            set
            {
                this.MutateVerbose(ref _message, value, RaisePropertyChanged());
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;
        private Action<PropertyChangedEventArgs> RaisePropertyChanged()
        {
            return args => PropertyChanged?.Invoke(this, args);
        }

        

    }
}
