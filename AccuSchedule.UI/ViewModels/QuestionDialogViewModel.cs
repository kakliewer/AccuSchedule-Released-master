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

namespace AccuSchedule.UI.ViewModels
{
    public class QuestionDialogViewModel : INotifyPropertyChanged
    {


        private string _question;
        public string Question
        {
            get { return _question; }
            set
            {
                this.MutateVerbose(ref _question, value, RaisePropertyChanged());
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;
        private Action<PropertyChangedEventArgs> RaisePropertyChanged()
        {
            return args => PropertyChanged?.Invoke(this, args);
        }

        public QuestionDialogViewModel(string question)
        {
            _question = question;
        }

    }
}
