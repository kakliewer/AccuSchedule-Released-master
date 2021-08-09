using AccuSchedule.UI.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Interfaces
{
    
    public interface IToolPlugin
    {
        string DefaultSection { get; }

        string NameOfSection(Type methodType);

        Type[] TypesToLoad { get; }


    }



    public abstract class ToolPlugin : IToolPlugin
    {
        private string defaultTitle = "Default";
        public virtual string DefaultSection => defaultTitle;


        public virtual Type[] TypesToLoad => null;

        public virtual string NameOfSection(Type methodType) => defaultTitle;

        public static object[] ObjectsToInject { get; set; }
        

        protected static void AddObjectToInject(object obj)
        {
            var injList = new List<object>();

            if (ObjectsToInject != null && ObjectsToInject.Any())
            {
                var objList = ObjectsToInject.ToList();
                injList.AddRange(objList);
            }

            var objJson = JsonConvert.SerializeObject(obj);

            bool addIt = true;
            foreach (var item in injList)
            {
                var comparedToJson = JsonConvert.SerializeObject(item);
                if (objJson == comparedToJson) { addIt = false; }
            }


            if (addIt && injList.Count > 0)
                injList.Add(obj);
            else if (injList.Count == 0)
                injList.Add(obj);

            ObjectsToInject = injList.ToArray();
        }
    }
}
