using AccuSchedule.UI.ViewModels;
using AccuSchedule.UI.ViewModels.VisualEditor;
using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
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
    public partial class ScopeView : IViewFor<ScopeViewModel>
    {

        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(ScopeViewModel), typeof(ScopeView), new PropertyMetadata(null));

        public ScopeViewModel ViewModel
        {
            get => (ScopeViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (ScopeViewModel)value;
        }
        #endregion

        public ScopeView()
        {
            InitializeComponent();
        }

        public void UpdateScope(ViewTabs vtab, bool clear = true) 
        {
            if (clear) stack.Children.Clear();

            if ((vtab.Table != null))
            {
                var node = vtab.Node as DefaultNodeViewModel;
                if (node?.TypeOfNode == DefaultNodeViewModel.NodeType.DataTable)
                {
                    var dNode = node as DataTableStartNode;
                    stack.Children.Add(new TextBlock() { Text = string.Format("Filename: {0}", dNode.FileName) });
                }
                    
                stack.Children.Add(new TextBlock() { Text = string.Format("Rows: {0}, Cols: {1}", vtab?.Table?.Rows?.Count, vtab?.Table?.Columns?.Count) });
            }
                
            else if ((vtab.Set != null))
            {
                stack.Children.Add(new TextBlock() { Text = string.Format("Tables: {0}", vtab?.Set?.Tables?.Count) });

                for (int i = 0; i < vtab.Set.Tables.Count; i++)
                    stack.Children.Add(new TextBlock() { Text = string.Format("Table {3}: {0}, Rows: {1}, Cols: {2}", vtab?.Set?.Tables[i].TableName, vtab?.Set?.Tables[i].Rows?.Count, vtab?.Set?.Tables[i].Columns?.Count, i) });
                
                    
            }
                
        }

        public void UpdateError(ParamTab ptab, Exception err)
        {
            stack.Children.Clear();

            UpdateScope(ptab.Tab, false);

            stack.Children.Add(new TextBlock() { Text = string.Format("Node: {0}", ptab.Tab.Header) });
            stack.Children.Add(new TextBlock() { Text = string.Format("Method: {0}", ptab.Tab.ToolMethod.ToolMethodInfo.Name) });
            stack.Children.Add(new TextBlock() { Text = string.Format("Parameters: {0}", string.Join(Environment.NewLine, ptab.Tab.Props.Values)) });
            stack.Children.Add(new TextBlock() { Text = string.Format("Error: {0}", err.Message) });

        }

    }

    
}
