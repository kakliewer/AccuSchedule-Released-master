using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared.Models.AccuplanXML
{
    [Serializable]
    public class FabricCodes
    {
        public string partnumber { get; set; }
        public int fabriccode { get; set; }
    }
}
