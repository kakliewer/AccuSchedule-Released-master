using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared.Models.Orders
{
    [Serializable]
    public class CrossReference : IEquatable<CrossReference>, IComparable<CrossReference>, ICloneable
    {

        public string PartNumber { get; set; }
        public string CrossReferencedPN { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public int CompareTo(CrossReference other)
        {
            return PartNumber.CompareTo(other.PartNumber);
        }

        public bool Equals(CrossReference other)
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
