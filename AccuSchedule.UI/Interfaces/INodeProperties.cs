using AccuSchedule.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Interfaces
{
    interface INodeProperties
    {
        ToolID Tool { get; set; }
        List<MemberInfo> InjectedObjects { get; set; }
    }

}
