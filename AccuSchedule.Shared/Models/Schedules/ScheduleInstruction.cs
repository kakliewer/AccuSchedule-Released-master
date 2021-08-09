using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared.Models.Schedules
{
    [Serializable]
    public class ScheduleInstruction
    {
        public string Name { get; set; }
        public string LaborKeyword { get; set; }
        public bool AdvIns { get; set; }
        public bool CreateCutTicket { get; set; }
        public string Options { get; set; }
        public HashSet<string> custNumFilter { get; set; }
        public HashSet<string> pnFilter { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public ScheduleInstruction(string laborKeyword, bool advancedIns, bool createCutTicket, string options, string name, HashSet<string> CustFilters, HashSet<string> PNFilters)
        {
            LaborKeyword = laborKeyword;
            AdvIns = advancedIns;
            CreateCutTicket = createCutTicket;
            Options = options;
            custNumFilter = CustFilters;
            pnFilter = PNFilters;
            Name = name;
        }
    }
}
