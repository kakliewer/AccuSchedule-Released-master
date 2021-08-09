using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.ViewModels
{
    [Serializable]
    public class ParamTab
    {
        [field: NonSerialized]
        public ViewTabs Tab { get; set; }
        [field: NonSerialized]
        public ParameterInfo ParamInfo { get; set; }

        public string ParamInfoName { get => ParamInfo?.Name; }
        public string Value { get; set; }
    }
}
