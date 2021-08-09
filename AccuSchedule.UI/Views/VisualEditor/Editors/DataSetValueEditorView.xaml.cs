using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

namespace AccuSchedule.UI.Views.VisualEditor.Editors
{
    /// <summary>
    /// Interaction logic for StringValueEditorView.xaml
    /// </summary>
    public partial class DataSetValueEditorView : IViewFor<DataSetEditorViewModel>
    {

        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(DataSetEditorViewModel), typeof(DataSetValueEditorView), new PropertyMetadata(null));

        public DataSetEditorViewModel ViewModel
        {
            get => (DataSetEditorViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (DataSetEditorViewModel)value;
        }
        #endregion

        public DataSetValueEditorView()
        {
            InitializeComponent();

            
        }
    }
}
