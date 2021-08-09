using AccuSchedule.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models
{
    [Serializable]
    public class ToolID
    {
        public string Category { get; set; }
        public Type ToolType { get; set; }
        public MethodInfo ToolMethodInfo { get; set; }

        public ViewTabs Payload { get; set; }
    }
}
