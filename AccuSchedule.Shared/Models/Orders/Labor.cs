using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared.Models.Orders
{
    [Serializable]
    public class Labor : IEquatable<Labor>, IComparable<Labor>, ICloneable
    {
        public double Sequence { get; set; }
        public string Description { get; set; }
        public double RMS_Per { get; set; }
        public double RLS_Per { get; set; }
        public double SLHS_Per { get; set; }
        public double RMS_Total { get; set; }
        public double RLS_Total { get; set; }
        public double SLHS_Total { get; set; }
        public int _lineQty { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public int CompareTo(Labor other)
        {
            return Description.CompareTo(other.Description);
        }

        public bool Equals(Labor other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return false;
        }
    }
}
