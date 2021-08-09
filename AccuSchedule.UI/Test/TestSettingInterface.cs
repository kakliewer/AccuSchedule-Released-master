using AccuSchedule.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AccuSchedule.UI.Test
{
    public class TestSettingInterface : ISetting
    {
        public string Category { get; set; }
        public Dictionary<string, FrameworkElement> Items { get; }

        public TestSettingInterface(string SettingName, params FrameworkElement[] ValueObj)
        {
            Items = new Dictionary<string, FrameworkElement>();

            Category = SettingName; // Change to pull from Category in real scenario

            // Setup Test Settings
            var sp = new StackPanel() { HorizontalAlignment = HorizontalAlignment.Stretch };

            foreach (var item in ValueObj) sp.Children.Add(item);

            Items.Add(SettingName, sp);
        }
    }
}
