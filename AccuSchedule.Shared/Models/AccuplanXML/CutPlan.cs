using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared.Models.AccuplanXML
{
    [Serializable]
    public class CutPlan
    {
        public string fabriccode { get; set; }
        public string[] vinylcolors { get; set; }
        public string vinylwidth { get; set; }
        public string vinylcost { get; set; }

        public string OrderSettings { get; set; } = "G-STORE";
        public string MarkerSettings { get; set; } = "SCHOOLBUS";
        public string CostSettings { get; set; } = "Default";
        public string SpreadSettings { get; set; } = "Default";

    }
}
