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
using ClosedXML.Report.Utils;

namespace AccuSchedule.UI.Plugins.Tools.Views
{
    /// <summary>
    /// Interaction logic for Preview.xaml
    /// </summary>
    public partial class HistoryModifications : MetroWindow
    {
        TextBox SearchBox { get; set; }

        public List<BoundList> HistoricalItems { get; set; }

        public List<string> ActionItems { get; set; } = new List<string>() { "Ignore", "Continue" };
        public bool Process { get; set; } = false;
        public System.Windows.Forms.Timer SearchTextBoxTimer { get; set; } = null;

        public string Status { get; set; } = "Status";

        public HistoryModifications(DataSet set)
        {
            InitializeComponent();
            this.DataContext = this;

            var pocoList = new HashSet<BoundList>();

            // Process the dataset into a bound POCO Class
            var currentTable = set.Tables["current"];
            foreach (var curRow in currentTable.AsEnumerable())
            {

                // Populate Current POCO
                var blCur = new BoundList(this)
                {
                    DateLogged = curRow.Field<object>("DateLogged").ToString(),
                    DateRequested = Convert.ToDateTime(curRow.Field<object>("DateRequested").ToString()).ToShortDateString(),
                    Customer = curRow.Field<object>("Customer").ToString(),
                    WONumber = curRow.Field<object>("WONumber").ToString(),
                    DateRevised = Convert.ToDateTime(curRow.Field<object>("DateRevised").ToString()).ToShortDateString(),
                    StatusCode = curRow.Field<object>("StatusCode").ToString(),
                    PN = curRow.Field<object>("PN").ToString(),
                    Qty = curRow.Field<object>("Qty").ToString(),
                    RelatedOrder = curRow.Field<object>("RelatedOrder").ToString(),
                    RelatedType = curRow.Field<object>("RelatedType").ToString()
                };

                // Populate the history
                var historicalMatches = curRow.GetChildRows("HistoricalMatches");
                var historyPoco = new HashSet<BoundListNoHist>();

                foreach (var historyRow in historicalMatches)
                {
                    var blHist = new BoundListNoHist()
                    {
                        DateLogged = historyRow.Field<object>("DateLogged").ToString(),
                        DateRequested = Convert.ToDateTime(historyRow.Field<object>("DateRequested").ToString()).ToShortDateString(),
                        Customer = historyRow.Field<object>("Customer").ToString(),
                        WONumber = historyRow.Field<object>("WONumber").ToString(),
                        DateRevised = Convert.ToDateTime(historyRow.Field<object>("DateRevised").ToString()).ToShortDateString(),
                        StatusCode = historyRow.Field<object>("StatusCode").ToString(),
                        PN = historyRow.Field<object>("PN").ToString(),
                        Qty = historyRow.Field<object>("Qty").ToString(),
                        RelatedOrder = historyRow.Field<object>("RelatedOrder").ToString(),
                        RelatedType = historyRow.Field<object>("RelatedType").ToString(),
                    };
                    historyPoco.Add(blHist);
                }

                // Populate blCur 'History' with blHist
                blCur.History = historyPoco;
                pocoList.Add(blCur);

            }

            HistoricalItems = pocoList.ToList();

            dgCurrent.ItemsSource = HistoricalItems.ToTable().AsDataView();

        }



        public class BoundList
        {
            public BoundList(HistoryModifications parent) => Action = parent.ActionItems.First();

            public string DateLogged { get; set; }
            public string DateRequested { get; set; }
            public string Customer { get; set; }
            public string WONumber { get; set; }
            public string DateRevised { get; set; }
            public string StatusCode { get; set; }
            public string RelatedOrder { get; set; }
            public string RelatedType { get; set; }
            public string PN { get; set; }
            public string Qty { get; set; }
            public string Action { get; set; } 

            public IEnumerable<BoundListNoHist> History { get; set; }
        }
        public class BoundListNoHist
        {
            public string DateLogged { get; set; }
            public string DateRequested { get; set; }
            public string Customer { get; set; }
            public string WONumber { get; set; }
            public string DateRevised { get; set; }
            public string StatusCode { get; set; }
            public string RelatedOrder { get; set; }
            public string RelatedType { get; set; }
            public string PN { get; set; }
            public string Qty { get; set; }
        }


        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {

            SearchBox = this.RightWindowCommands.FindChild<TextBox>("txtSearch") as TextBox;
            if (string.IsNullOrEmpty(SearchBox.Text)) SearchBox.Text = "Search Plan...";

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
        private void txtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var grid = dgCurrent;
                try
                {
                    if (grid != null)
                    {
                        var dataview = grid.ItemsSource as DataView;
                        if (string.IsNullOrEmpty(txtSearch.Text))
                            dataview.RowFilter = "";
                        else
                            dataview.RowFilter = Extensions.DataTableExtensions.CreateTableSearchQuery(dataview.Table, txtSearch.Text);
                    }
                }
                catch (Exception)
                {
                }
                if (SearchTextBoxTimer != null)
                {
                    SearchTextBoxTimer.Stop();
                    SearchTextBoxTimer.Dispose();
                    SearchTextBoxTimer = null;
                }
                return;
            }
            else
            {
                if (SearchTextBoxTimer != null)
                {
                    if (SearchTextBoxTimer.Interval < 750)
                        SearchTextBoxTimer.Interval += 750;
                }
                else
                {
                    SearchTextBoxTimer = new System.Windows.Forms.Timer();
                    SearchTextBoxTimer.Tick += new EventHandler(SearchTextBoxTimer_Tick);
                    SearchTextBoxTimer.Interval = 500;
                    SearchTextBoxTimer.Start();
                }
            }
        }

        private void SearchTextBoxTimer_Tick(object sender, EventArgs e)
        {
            var grid = dgCurrent;
            try
            {
                if (grid != null)
                {
                    var dataview = grid.ItemsSource as DataView;
                    if (string.IsNullOrEmpty(txtSearch.Text))
                        dataview.RowFilter = "";
                    else
                        dataview.RowFilter = Extensions.DataTableExtensions.CreateTableSearchQuery(dataview.Table, txtSearch.Text);
                }
            }
            catch (Exception)
            {
            }


            SearchTextBoxTimer.Stop();
            SearchTextBoxTimer.Dispose();
            SearchTextBoxTimer = null;
        }


        private void Process_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process = true;
            this.Close();
        }
    }
}
