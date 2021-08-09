using AccuSchedule.UI.Extensions;
using AccuSchedule.UI.ViewModels;
using AccuSchedule.UI.ViewModels.VisualEditor;
using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
using AccuSchedule.UI.Views.VisualEditor;
using MahApps.Metro.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Data;
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

namespace AccuSchedule.UI.Views
{
    /// <summary>
    /// Interaction logic for ScopeView.xaml
    /// </summary>
    public partial class PropertiesView : IViewFor<PropertiesViewModel>
    {

        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(PropertiesViewModel), typeof(PropertiesView), new PropertyMetadata(null));

        public PropertiesViewModel ViewModel
        {
            get => (PropertiesViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (PropertiesViewModel)value;
        }
        #endregion


        public delegate void PropEvent(UIElement element);
        public event PropEvent LostPropFocus;

        public PropertiesView()
        {
            InitializeComponent();
        }

        public IDictionary<ExportNode, IEnumerable<object>> VoidNodesAndChainedObjects { get; set; }


        public void UpdateProperties(List<UIElement> elements, Dictionary<string, ParamTab> props)
        {
            stack.Children.Clear();

            if (elements == null || !elements.Any()) return;

            int propCount = 0;

            // Modify the textbox per saved values
            foreach (var element in elements)
            {
                var sp = element as StackPanel;
                foreach (var child in sp.Children)
                {
                    var targetTxtBox = child as TextBox;
                    var targetComboBox = child as ComboBox;

                    if (targetTxtBox != null)
                    {
                        if (props != null && props.Any()
                            && props.ContainsKey(targetTxtBox.Name))
                        {
                            targetTxtBox.Text = props[targetTxtBox.Name].Value;
                            propCount += 1;
                        }
                            
                        targetTxtBox.LostFocus += InputParam_LostFocus;
                        
                    } else if(targetComboBox != null)
                    {
                        if (props != null && props.Any()
                            && props.ContainsKey(targetComboBox.Name))
                        {
                            targetComboBox.SelectedItem = props[targetComboBox.Name].Value;
                            if (string.IsNullOrEmpty(targetComboBox.Text)) // If the value is edited then this will add just the text
                                targetComboBox.Text = props[targetComboBox.Name].Value;
                            propCount += 1;
                        }

                        targetComboBox.LostFocus += InputParam_LostFocus;
                    }
                }

                // Add the control set
                stack.Children.Add(element);


            }
            
        }

        private void InputParam_LostFocus(object sender, RoutedEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox != null)
                LostPropFocus(txtBox);

            var cmboBox = sender as ComboBox;
            if (cmboBox != null)
                LostPropFocus(cmboBox);

        }
    }

    
}
