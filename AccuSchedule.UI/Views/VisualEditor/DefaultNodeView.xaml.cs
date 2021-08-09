using AccuSchedule.UI.ViewModels.VisualEditor;
using NodeNetwork.Views;
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
using System.Reactive.Disposables;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultNodeViewModel;
using System.Security.Cryptography;

namespace AccuSchedule.UI.Views.VisualEditor
{
    /// <summary>
    /// Interaction logic for DefaultNodeView.xaml
    /// </summary>
    public partial class DefaultNodeView : IViewFor<DefaultNodeViewModel>
    {
        public DefaultNodeView()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                NodeView.ViewModel = this.ViewModel;
                Disposable.Create(() => NodeView.ViewModel = null).DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.TypeOfNode, v => v.NodeView.Background, ConvertNodeTypeToBrush).DisposeWith(d);

            });
        }


        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(DefaultNodeViewModel), typeof(DefaultNodeView), new PropertyMetadata(null));

        public DefaultNodeViewModel ViewModel { 
            get => (DefaultNodeViewModel)GetValue(ViewModelProperty); 
            set => SetValue(ViewModelProperty, value); 
        }
        object IViewFor.ViewModel { 
            get => ViewModel; 
            set => SetValue(ViewModelProperty, value); 
        }

        

        private Brush ConvertNodeTypeToBrush(NodeType type)
        {
            switch (type)
            {
                case NodeType.EventNode: return new SolidColorBrush(Color.FromRgb(0x9b, 0x00, 0x00));
                case NodeType.FlowControl: return new SolidColorBrush(Color.FromRgb(0x49, 0x49, 0x49));
                case NodeType.Function: return new SolidColorBrush(Color.FromRgb(0x00, 0x39, 0xcb));
                case NodeType.Literal: return new SolidColorBrush(Color.FromRgb(0x00, 0x60, 0x0f));

                case NodeType.DataTable: return new SolidColorBrush(Color.FromRgb(255, 58, 190));
                case NodeType.DataSet: return new SolidColorBrush(Color.FromRgb(0, 174, 219));
                case NodeType.TableProcessing: return new SolidColorBrush(Color.FromRgb(243, 119, 53));
                case NodeType.DataSetFromTable: return new SolidColorBrush(Color.FromRgb(209, 17, 65));
                case NodeType.DataTableFromSet: return new SolidColorBrush(Color.FromRgb(209, 17, 161));
                case NodeType.Void: return new SolidColorBrush(Color.FromRgb(0, 177, 89));
                case NodeType.ObjList: return new SolidColorBrush(Color.FromRgb(196, 144, 0));
                default: throw new Exception("Unsupported node type");
            }
        }

        static string GetColor(string raw)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(data).Replace("-", string.Empty).Substring(0, 6);
            }
        }



    }
}
