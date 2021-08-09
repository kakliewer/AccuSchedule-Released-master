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
    public class SaveProjectDialogViewModel : INotifyPropertyChanged
    {

        public enum SaveAsEnum
        {
            Template,
            Project
        }

        private string _projectName;
        public string ProjectName
        {
            get { return _projectName; }
            set
            {
                this.MutateVerbose(ref _projectName, value, RaisePropertyChanged());
            }
        }



        private string _filePath;
        public string FilePath
        {
            get { return _filePath; }
            set
            {
                this.MutateVerbose(ref _filePath, value, RaisePropertyChanged());
            }
        }

        private string _saveAs = SaveAsEnum.Template.ToString();
        public string SaveAs
        {
            get { return _saveAs; }
            set
            {
                this.MutateVerbose(ref _saveAs, value, RaisePropertyChanged());
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private Action<PropertyChangedEventArgs> RaisePropertyChanged()
        {
            return args => PropertyChanged?.Invoke(this, args);
        }

        

    }
}
