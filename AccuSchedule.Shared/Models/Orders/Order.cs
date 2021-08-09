using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AccuSchedule.Shared.Models.Orders
{
    [Serializable]
    public class Order : IEquatable<Order>, IComparable<Order>, ICloneable
    {
        public List<Part> PartsOnOrder { get; set; }
        public string Order_Number { get; set; }
        public string Customer { get; set; }
        public string CustomerNumber { get; set; }
        public double CostCenter { get; set; }
        public DateTime DateRequested { get; set; }
        public string PickSlipNum { get; set; }
        public double OrigTotalRawUsage { get; set; }
        public double TotalRawUsage { get; set; }
        public double TotalLabor { get; set; }
        public string OrderID { get; set; }
        public bool Checked { get; set; } = false;


        // Used to determine Method of handling
        public string Opt1 { get; set; }
        public string Opt2 { get; set; }

        public object Clone()
        {

            return this.MemberwiseClone();
        }

        public int CompareTo(Order other)
        {
            return Order_Number.CompareTo(other.Order_Number);
        }

        public bool Equals(Order other)
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
