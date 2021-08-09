using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Interfaces
{
    public interface IPreviewTable
    {
        IEnumerable<String> Models { get; set; }
    }

    public abstract class PreviewTable : IPreviewTable
    {
        public virtual IEnumerable<string> Models { get; set; }
    }
}
