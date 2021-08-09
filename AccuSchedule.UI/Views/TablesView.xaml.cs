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
    /// Interaction logic for TablesView.xaml
    /// </summary>
    public partial class TablesView : IViewFor<TablesViewModel>
    {
        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(TablesViewModel), typeof(TablesView), new PropertyMetadata(null));

        public TablesViewModel ViewModel
        {
            get => (TablesViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (TablesViewModel)value;
        }
        #endregion


        public TablesView()
        {
            InitializeComponent();


        }

        public void AddTab(ViewTabs table)
        {
            if (!CheckIfTabExists(table.Table.TableName))
            {
                var dg = new DataGrid() { ItemsSource = table.dataView };
                tabs.Items.Add(new TabItem() { Header = table.Header, Content = dg });
            }
                
        }

        public void AddNodeToList(DataTable table, MainViewModel VM, string File)
        {
            var dt = new DataTableStartNode(table, File);
            VM.NodeList.AddNodeType(() => dt);
        }

        public bool CheckIfTabExists(string tableName)
        {
            foreach (TabItem item in tabs.Items)
                if (item.Header.ToString() == tableName)
                    return true;

            return false;
        }

        public void UpdateTab(ViewTabs tab)
        {
            bool found = false;
            if (tab.Table != null)
            {
                var table = tab.Table;
                foreach (TabItem item in tabs.Items)
                {
                    if (item.Header.ToString() == table.TableName)
                    {
                        var dg = item.Content as DataGrid;
                        if (dg != null)
                        {
                            found = true;
                            dg.ItemsSource = null;
                            dg.ItemsSource = table.DefaultView;
                        }
                    }
                }

                // If it wasn't found, then add the new table into view
                if (!found) AddTab(new ViewTabs() { Header = table.TableName, Table = table });
            }
            if (tab.Set != null)
            {
                foreach (DataTable item in tab.Set.Tables)
                {
                    var tabItem = tabs.Items.OfType<TabItem>().SingleOrDefault(n => n.Header.ToString() == item.TableName);
                    if (tabItem != null)
                    { // Update existing tabs
                        var dg = tabItem.Content as DataGrid;
                        if (dg != null)
                        {
                            found = true;
                            dg.ItemsSource = null;
                            dg.ItemsSource = item.DefaultView;
                        }
                    } else
                    { // Add tab for new table if doesn't exist
                        var dg = new DataGrid() { ItemsSource = item.DefaultView };
                        var ti = new TabItem() { Header = item.TableName, Content = dg };
                        tabs.Items.Add(ti);
                    }
                }
            }
        }

        public void RemoveTab(DataTable table)
        {
            var toRemove = new HashSet<TabItem>();

            foreach (TabItem item in tabs.Items) 
                if (item.Header.ToString() == table?.TableName) 
                    toRemove.Add(item);

            foreach (var item in toRemove)
                tabs.Items.Remove(item);
        }

        public DataTable GetTable(string tableName)
        {
            foreach (TabItem item in tabs.Items)
            {
                if (item.Header.ToString() == tableName)
                {
                    var dg = item.Content as DataGrid;
                    if (dg != null)
                    {
                        var itmSource = dg.ItemsSource as DataView;
                        return itmSource.Table;
                    }
                }
            }

            return null;
        }


    }
}
