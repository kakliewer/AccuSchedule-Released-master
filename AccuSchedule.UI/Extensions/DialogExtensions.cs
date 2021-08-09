using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AccuSchedule.UI.Extensions
{
    public static class DialogExtensions
    {
        public static async Task<T> ShowAsDialog<T>(this UserControl dialogView, T dialogContext, DialogHost host)
        {

            //let's set up a little MVVM, cos that's what the cool kids are doing:
            if (dialogView != null)
            {
                dialogView.DataContext = dialogContext;

                //show the dialog
                var res = await host.ShowDialog(dialogView) as bool?;
                if (res ?? false)
                {
                    var results = (T)dialogView.DataContext;
                    if (results != null)
                        return results;
                }
            }

            return default(T);
        }
    }
}
