using AccuSchedule.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AccuSchedule.UI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for SaveProjectDialog.xaml
    /// </summary>
    public partial class SaveProjectDialog : UserControl
    {
        public SaveProjectDialog()
        {
            InitializeComponent();
        }

        private void GetDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dir = @"\\Store\hsmc$\213\dept\ENG\import\";

            using (var dialog = new System.Windows.Forms.FolderBrowserDialog() { SelectedPath = dir})
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                FilePath.Text = dialog.SelectedPath;
            }
        }
    }
}
