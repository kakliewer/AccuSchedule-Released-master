using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared.Models.Orders
{
    [Serializable]
    public class Material : IEquatable<Material>, IComparable<Material>, ICloneable
    {
        public string VinylPN { get; set; }
        public string VinylDesc1 { get; set; }
        public string VinylDesc2 { get; set; }
        public double Required_YD_Per { get; set; }
        public double Required_YD_Total { get; set; }
        public double VinylWidth { get; set; }

        public string FabricCode { get; set; }
        public string BOMToModelTabCode { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public int CompareTo(Material other)
        {
            return VinylPN.CompareTo(other.VinylPN);
        }

        public bool Equals(Material other)
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
