using Dragablz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AccuSchedule.UI.Interfaces
{
    public class ITabDragClient : IInterTabClient
    {
        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }

        INewTabHost<Window> IInterTabClient.GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            var view = new MainWindow();
            return new NewTabHost<MainWindow>(view, new TabablzControl()); //TabablzControl is a names control in the XAML
        }
    }
}
