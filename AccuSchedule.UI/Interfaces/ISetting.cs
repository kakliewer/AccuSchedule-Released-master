using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AccuSchedule.UI.Interfaces
{
    public interface ISetting
    {
        string Category { get; }

        Dictionary<string, FrameworkElement> Items { get; }
    }

}
