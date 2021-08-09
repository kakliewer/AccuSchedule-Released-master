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
    /// Interaction logic for DefaultPendingConnectionView.xaml
    /// </summary>
    public partial class DefaultPendingConnectionView : IViewFor<DefaultPendingConnectionViewModel>
    {

        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(DefaultPendingConnectionViewModel), typeof(DefaultPendingConnectionView), new PropertyMetadata(null));

        public DefaultPendingConnectionViewModel ViewModel
        {
            get => (DefaultPendingConnectionViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (DefaultPendingConnectionViewModel)value;
        }
        #endregion

        public DefaultPendingConnectionView()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                PendingConnectionView.ViewModel = this.ViewModel;
                d(Disposable.Create(() => PendingConnectionView.ViewModel = null));
            });
        }
    }
}
