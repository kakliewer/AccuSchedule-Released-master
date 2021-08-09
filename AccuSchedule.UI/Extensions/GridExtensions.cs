using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AccuSchedule.UI.Extensions
{
    public static class GridExtensions
    {
        public static void InsertToCell(this Grid grid, int col, int row, UIElement content, bool clearContents = false)
        {
            if (clearContents) grid.Children.Clear();

            Grid.SetColumn(content, col);
            Grid.SetRow(content, row);

            grid.Children.Add(content);
           
        }

        public static void FilterGridBySearchText(this DataGrid grid, string searchText)
        {
            var dataview = grid.ItemsSource as DataView;

            dataview.RowFilter = DataTableExtensions.CreateTableSearchQuery(dataview.Table, searchText);

            return;

        }
        
        

        public static DataGrid FindActiveGrid(TabControl tabControl)
        {
            var focusedIndex = tabControl.SelectedIndex;
            if (focusedIndex < 0) return null;

            var focusedTab = tabControl.Items[focusedIndex] as TabItem;

            var focusedGrid = focusedTab.Content as DataGrid;
            return focusedGrid;

        }

    }
}
