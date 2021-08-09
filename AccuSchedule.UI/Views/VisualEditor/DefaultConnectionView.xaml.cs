using AccuSchedule.UI.ViewModels.VisualEditor;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
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

namespace AccuSchedule.UI.Views.VisualEditor
{
    /// <summary>
    /// Interaction logic for DefaultConnectionView.xaml
    /// </summary>
    public partial class DefaultConnectionView : IViewFor<DefaultConnectionViewModel>
    {
        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(DefaultConnectionViewModel), typeof(DefaultConnectionView), new PropertyMetadata(null));

        public DefaultConnectionViewModel ViewModel
        {
            get => (DefaultConnectionViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (DefaultConnectionViewModel)value;
        }
        #endregion

        public DefaultConnectionView()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                ConnectionView.ViewModel = this.ViewModel;
                d(Disposable.Create(() => ConnectionView.ViewModel = null));
            });
        }
    }
}
