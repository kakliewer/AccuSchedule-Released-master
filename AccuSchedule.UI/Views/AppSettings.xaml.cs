
using System.Collections.Generic;

using System.Windows;
using System.Windows.Controls;

using MahApps.Metro.Controls;

using AccuSchedule.UI.Interfaces;
using System.Reflection;
using AccuSchedule.UI.Test;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using AccuSchedule.UI.Extensions;

namespace AccuSchedule.UI.Views
{
    /// <summary>
    /// Interaction logic for AppSettings.xaml
    /// </summary>
    public partial class AppSettings : MetroWindow
    {
        TextBox SearchBox { get; set; }

        public ObservableCollection<string> CategoryNames { get; set; }
        List<ISetting> GlobalSettings { get; set; }  

        StackPanel RHViewPanel { get; set; }
        ListView LHMenu { get; set; }

        public AppSettings(List<ISetting> Settings)
        {
            InitializeComponent();

            

            GlobalSettings = Settings;
            CategoryNames = new ObservableCollection<string>();

            CreateMenu();
            CreateRHView();
        }

        public void CreateMenu()
        {

            var MenuPanel = new StackPanel() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
            LHMenu = new ListView();
            MenuPanel.Children.Add(LHMenu);
            DockPanel.SetDock(MenuPanel, Dock.Left);

            // Add the Categories
            foreach (var setting in GlobalSettings) CategoryNames.Add(setting.Category);
            CategoryNames.Distinct();
            LHMenu.ItemsSource = CategoryNames;
            LHMenu.MouseUp += Categories_MouseUp;

            RootGrid.Children.Add(MenuPanel);

        }
        public void CreateRHView()
        {
            RHViewPanel = new StackPanel() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
            
            LHMenu.SelectedIndex = 0;
            Categories_MouseUp(LHMenu, null);

            RootGrid.Children.Add(RHViewPanel);
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SearchBox = this.RightWindowCommands.FindChild<TextBox>("txtSearch") as TextBox;
            if (string.IsNullOrEmpty(SearchBox.Text)) SearchBox.Text = "Search Settings...";

        }


        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb.Text == "Search Settings...") tb.Text = string.Empty;
        }
        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = "Search Settings...";
            }
        }

        private void Categories_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv != null)
            {
                // Get the selection settings
                var settingsToShow = GlobalSettings.Where(w => w.Category == lv.SelectedValue.ToString()).Select(s => s.Items);
                RHViewPanel.Children.Clear();

                foreach (var setting in settingsToShow)
                    foreach (var item in setting.Values)
                    {
                        if (!RHViewPanel.Children.Contains(item))
                            RHViewPanel.Children.Add(item);
                    }
                            
            }
        }
    }
}
