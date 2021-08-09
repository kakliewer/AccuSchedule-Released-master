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
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AccuSchedule.UI.Views.VisualEditor
{
    /// <summary>
    /// Interaction logic for DefaultPortView.xaml
    /// </summary>
    public partial class DefaultPortView : IViewFor<DefaultPortViewModel>
    {

        public const string ExecutionPortTemplateKey = "ExecutionPortTemplate";
        public const string IntegerPortTemplateKey = "IntegerPortTemplate";
        public const string StringPortTemplateKey = "StringPortTemplate";
        public const string DataTablePortTemplateKey = "DataTablePortTemplate";
        public const string DataTableProcessingPortTemplate = "DataTableProcessingPortTemplate";
        public const string DataSetPortTemplate = "DataSetPortTemplate";

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(DefaultPortViewModel), typeof(DefaultPortView), new PropertyMetadata(null));

        public DefaultPortViewModel ViewModel
        {
            get => (DefaultPortViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (DefaultPortViewModel)value;
        }


        public DefaultPortView()
        {
            InitializeComponent();


            (this).WhenActivated((Action<CompositeDisposable>)(d =>
            {
                (this).WhenAnyValue(v => v.ViewModel).BindTo(this, v => v.PortView.ViewModel).DisposeWith(d);

                (this).OneWayBind(ViewModel, (System.Linq.Expressions.Expression<Func<DefaultPortViewModel, DefaultPortViewModel.PortType>>)(vm => (DefaultPortViewModel.PortType)vm.TypeOfPort), v => v.PortView.Template, this.GetTemplateFromPortType)
                    .DisposeWith(d);

                (this).OneWayBind(ViewModel, vm => vm.IsMirrored, v => v.PortView.RenderTransform,
                    isMirrored => new ScaleTransform(isMirrored ? -1.0 : 1.0, 1.0))
                    .DisposeWith(d);
            }));

            

        }

        public ControlTemplate GetTemplateFromPortType(DefaultPortViewModel.PortType type)
        {
            switch (type)
            {
                case DefaultPortViewModel.PortType.Execution: return (ControlTemplate)Resources[ExecutionPortTemplateKey];
                case DefaultPortViewModel.PortType.Integer: return (ControlTemplate)Resources[IntegerPortTemplateKey];
                case DefaultPortViewModel.PortType.String: return (ControlTemplate)Resources[StringPortTemplateKey];
                case DefaultPortViewModel.PortType.DataTable: return (ControlTemplate)Resources[DataTablePortTemplateKey];
                case DefaultPortViewModel.PortType.DataTableProcessing: return (ControlTemplate)Resources[DataTableProcessingPortTemplate];
                case DefaultPortViewModel.PortType.DataSet: return (ControlTemplate)Resources[DataSetPortTemplate];
                default: throw new Exception("Unsupported port type");
            }
        }
    }
}
