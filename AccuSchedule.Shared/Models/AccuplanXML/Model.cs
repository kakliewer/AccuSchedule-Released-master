using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared.Models.AccuplanXML
{
    [Serializable]
    public class Model
    {
        public string partnumber { get; set; }
        public string[] fabriccodes { get; set; }
        public string fabcodescombined { get { return string.Join(",", fabriccodes); } }
        public int sizeid { get; set; }
        public int size { get; set; }
        public string storagearea { get; set; }
        public bool hasoptions
        {
            get
            {
                if (options == null || !options.Any() || options.Count() == 0) return false;
                return true;
            }
        }

        public string[] options { get; set; }
    }
}
