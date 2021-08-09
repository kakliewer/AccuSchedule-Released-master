using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AccuSchedule.Shared.Models.Orders
{
    [Serializable]
    public class Part : IEquatable<Part>, IComparable<Part>, ICloneable
    {
        public enum Flagged
        {
            Ignore = 1,
            MissingXref = 2,
            MissingMarker = 3,
            MissingCutPlan = 4,
            MissingLabor = 5,
            MissingMaterial = 6
        }

        public string PartNumber { get; set; }
        public string XRef_PartNumber { get; set; }
        public string Description1 { get; set; }
        public string Description2 { get; set; }
        public string LineType { get; set; }
        public int Quantity { get; set; }
        public double Total_YD_Required { get; set; }
        public double Total_Labor_Required { get; set; }
        public double StatusCodeLast { get; set; }
        public double StatusCodeNext { get; set; }
        public HashSet<Flagged> Flags { get; set; } = new HashSet<Flagged>();

        public ICollection<Part> Labor { get; set; }
        public ICollection<Part> Vinyl { get; set; }

        public int _SizeId { get; set; } // Used for Accuplan

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public int CompareTo(Part other)
        {
            return PartNumber.CompareTo(other.PartNumber);
        }

        public bool Equals(Part other)
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
