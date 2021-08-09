using ClosedXML.Excel;
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
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using System.Data;
using System.Collections;
using AccuSchedule.UI.Extensions;
using System.Collections.ObjectModel;
using System.Dynamic;
using ImpromptuInterface;
using AccuSchedule.UI.Methods;
using Jint.Native.Object;
using Jint.Native;
using System.Web.Script.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using AccuSchedule.UI.Interfaces;
using System.Reflection;
using AccuSchedule.UI.Test;
using ClosedXML.Report;

namespace AccuSchedule.UI.Views
{
    /// <summary>
    /// Interaction logic for Preview.xaml
    /// </summary>
    public partial class Preview : MetroWindow
    {

        List<IXLTable> _tables { get; set; }
        TabControl Tabs { get; set; }
        TextBox SearchBox { get; set; }

        List<DataTable> Tables { get; set; }


        public Preview(List<IXLTable> cXMLtables, string FileName, IProcessingWindow ProcessingWindow)
        {
            InitializeComponent();
            this.Owner = ProcessingWindow as MetroWindow;
           
            Tables = new List<DataTable>();
       
            // Set the file name
            lblFileName.Text = FileName;
            if (Tables == null)
            {
                lblFileName.Text = string.Format("0 Tables Found in {0}!", FileName);
                return;
            }

            // Add Tab Control to window
            _tables = cXMLtables;
            Tabs = new TabControl() {
                VerticalAlignment = VerticalAlignment.Stretch, 
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            
            RootGrid.Children.Add(Tabs);

            // Populate Tab Control
            if (_tables != null) foreach (var table in _tables) Tabs.Items.Add(CreateTabView(table));

            if (_tables == null || _tables.Count == 0) return; // Exit if no tables were found

            // Set focus to first tab
            if (Tabs.Items.Count > 0)
            {
                var ti = (TabItem)Tabs.Items[0];
                Tabs_GotFocus(ti, null);
            }
        }


        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {

            SearchBox = this.RightWindowCommands.FindChild<TextBox>("txtSearch") as TextBox;
            if (string.IsNullOrEmpty(SearchBox.Text)) SearchBox.Text = "Search Plan...";

        }

        public TabItem CreateTabView(IXLTable table)
        {

            // Add new Tab Item for each table
            var gridView = new Grid();
            var dataGrid = CreateNewDataGrid(table);
            dataGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            dataGrid.VerticalAlignment = VerticalAlignment.Stretch;
            gridView.Children.Add(dataGrid);

            var newItem = new TabItem() { Header = table?.Name , Content = gridView };
            newItem.MouseRightButtonUp += NewItem_MouseRightButtonUp;
            

            return newItem;
        }

        private void NewItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = sender as TabItem;
            if (item != null)
            {
                var headerText = item.Header.ToString();
                item.Tag = headerText;
                var renameTB = new TextBox() { Text = headerText, Background = Brushes.Transparent };
                renameTB.LostFocus += TabRenameText_LostFocus;
                renameTB.KeyUp += TabRenameText_KeyUp;
                item.Header = renameTB;
                
            }
        }

        private void TabRenameText_KeyUp(object sender, KeyEventArgs e)
        {
            var item = sender as TextBox;
            TabItem tabItem = null;
            if (item != null) tabItem = item.Parent as TabItem;
            if (tabItem != null)
                if (e.Key == Key.Enter) RenameTab(item, tabItem);
        }

        private void TabRenameText_LostFocus(object sender, RoutedEventArgs e)
        {
            var item = sender as TextBox;
            TabItem tabItem = null;
            if (item != null) tabItem = item.Parent as TabItem;
            RenameTab(item, tabItem);
            e.Handled = true;
        }

        private void RenameTab(TextBox item, TabItem tabItem)
        {
            if (item != null && tabItem != null)
            {
                if (!string.IsNullOrEmpty(item.Text))
                    tabItem.Header = item.Text;
                else
                    tabItem.Header = tabItem.Tag;

                // Rename the _tables accordingly
                string oldName = tabItem.Tag.ToString();
                Tables.Where(table => table.TableName == oldName).FirstOrDefault().TableName = tabItem.Header.ToString();

                tabItem.Focus();
            }
        }

        private void Tabs_GotFocus(object sender, RoutedEventArgs e)
        {
            var tabItem = sender as TabItem;
            if (sender == null) return;

            // Find the matching table
            var table = GetActiveTable(tabItem.Header.ToString());

            if (table != null) lblRowsCols.Text = string.Format("Cols: {0}, Rows: {1}", table.DefaultView.Table.Columns.Count, table.DefaultView.Table.Rows.Count);

            if (e != null) e.Handled = true;
        }

        public DataGrid CreateNewDataGrid(IXLTable table)
        {
            // Cast Excel Table to list of dynamic objects
            //List<ExpandoObject> dt = table.AsDynamicEnumerable().Select(s => s as ExpandoObject).ToList();

            // Setup a new static that replicates the dynamic object using Impormptu
            //IEnumerable<dynamic> proxiedObject = dt.Select(x =>
            //    Impromptu.ActLikeProperties(x, x.ToDictionary(k => k.Key, v => typeof(object))));
            var dt = table.AsNativeDataTable();
            Tables.Add(dt);

            // Write to data grid
            var dg = new DataGrid() {
                ItemsSource = dt.DefaultView
                , Name = "preview"
                , AlternationCount = dt.DefaultView.Count
                , GridLinesVisibility = DataGridGridLinesVisibility.Vertical
                , CanUserSortColumns = true
                ,CanUserResizeColumns = true
                , CanUserAddRows = false
                , CanUserDeleteRows = false
                , CanUserReorderColumns = true
                , CanUserResizeRows = true
            };

            dg.AutoGeneratedColumns += Dg_AutoGeneratedColumns;

            return dg;
         }

        private void Dg_AutoGeneratedColumns(object sender, EventArgs e)
        {
            // Adds the col and row count after the columns have been generated
            DataGrid dataGrid = sender as DataGrid;
            if (dataGrid != null && dataGrid.IsVisible) lblRowsCols.Text = string.Format("Cols: {0}, Rows: {1}", dataGrid.Columns.Count, dataGrid.Items.Count);
        }


        public bool SearchObject(dynamic dynObj)
        {
            foreach (KeyValuePair<string, object> kvp in ((IDictionary<string, object>)dynObj))
                if (!kvp.Value.GetType().Name.Contains("BB")) return false;

            return true;
        }

        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb.Text == "Search Table...") tb.Text = string.Empty;
        }
        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = "Search Table...";
            }
        }


        private string GetActiveTabName()
        {
            var tabName = string.Empty;
            foreach (TabItem tab in Tabs.Items)
                if (tab.IsSelected) tabName = tab.Header.ToString();
            return tabName;
        }
        private DataTable GetActiveTable(string activeTabName) => Tables.Where(w => w.TableName == activeTabName).FirstOrDefault();
        private void lblExport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Find the matching table
            var table = GetActiveTable(GetActiveTabName());

            // Send it to the Template Engine via Extension
            TemplateErrors errors = null;
            if (table != null)
            {
                errors = table.DefaultView.ToTemplateEngine();
            } else
            {
            }
        }
        private void lblProcess_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var table = GetActiveTable(GetActiveTabName());
            var processInterface = this.Owner as IProcessingWindow;

            // Execute the Processor
            processInterface.ProcessingWindow.Focus();
            processInterface.AddToDataSet(table, lblFileName.Text);


        }
    }
}
