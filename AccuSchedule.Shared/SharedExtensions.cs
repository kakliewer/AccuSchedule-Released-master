using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.Shared
{
    public static class SharedExtensions
    {
        public static dynamic TranslateJsonDefinitionToAnonObject(dynamic def, dynamic obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            var translation = JsonConvert.DeserializeAnonymousType(json, def);

            return translation;
        }

        
    }
}
