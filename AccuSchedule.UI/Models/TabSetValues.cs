using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models
{
    public class TabSetValues : List<ViewTabs>
    {
        public ObservableCollection<ViewTabs> Value { get; } = new ObservableCollection<ViewTabs>();

        public string Compile(CompilerContext ctx)
        {
            return "List=" + Value.Count();
        }

    }
}
