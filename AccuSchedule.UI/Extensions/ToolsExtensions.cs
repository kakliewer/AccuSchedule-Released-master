using ClosedXML.Report;
using Jint.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Extensions
{
    public static class ToolsExtensions
    {
        public static object GetMemberValue(this MemberInfo memberInfo, object forObject = null)
        {
            switch (memberInfo.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)memberInfo).GetValue(forObject);
                case MemberTypes.Property:
                    return ((PropertyInfo)memberInfo).GetValue(forObject);
                case MemberTypes.Method:
                    return memberInfo.GetMemberValue(forObject);
                default:
                    throw new NotImplementedException();
            }
        }



        public static string DynamicGroup<DataD>(IGrouping<dynamic, DataD> group)
        {
            string result = null;
            foreach (System.Reflection.PropertyInfo pInfo in typeof(DataD).GetProperties().Where(x => x.Name != "GroupID" && x.Name != "ID"))
            {
                result += string.Format(" {0} ; ", string.Join(",", group.Select((k) => pInfo.GetValue(k, null)).Distinct()));
            }
            return result;
        }

        public static IEnumerable<T> Flatten<T>(
            this IEnumerable<T> e
        , Func<T, IEnumerable<T>> f
        ) => e.SelectMany(c => f(c).Flatten(f)).Concat(e);

        public static void AddVarToEngineWithBindingSource(this XLTemplate engine, object obj)
        {
            var ds = obj as DataSet;
            if (ds != null)
            { // Add each table with a binding source so relationships are populated
                foreach (DataTable table in ds.Tables)
                {
                    if (obj != null && !string.IsNullOrEmpty(table.TableName))
                    {
                        System.Windows.Forms.BindingSource bs = new System.Windows.Forms.BindingSource() { DataSource = table };
                        engine?.AddVariable(table.TableName, bs);
                    }

                }

            }
            else if (obj.GetType() == typeof(DataTable))
            { // Add table with binding source
                var objName = obj.GetObjectName();
                if (obj != null && !string.IsNullOrEmpty(objName))
                {
                    System.Windows.Forms.BindingSource bs = new System.Windows.Forms.BindingSource() { DataSource = obj };
                    engine?.AddVariable(objName, bs);
                }
            }
        }

        public static bool IsSameObjectByName(object objA, object objB)
        {
            if (objA?.GetType() != objB?.GetType()) return false;

            var dtA = objA as DataTable;
            if (dtA != null)
            {
                var dtB = objB as DataTable;
                if (dtA.TableName == dtB.TableName) return true;
            }

            var dsA = objA as DataSet;
            if (dsA != null)
            {
                var dsB = objB as DataSet;
                if (dsA.DataSetName == dsB.DataSetName) return true;
            }


            return false;
        }

        public static string GenerateID(string BranchID = null)
        {
            Thread.Sleep(1);//make everything unique while looping
            long ticks = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0))).TotalMilliseconds;//EPOCH
            char[] baseChars = new char[] { '0','1','2','3','4','5','6','7','8','9',
            'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z'};

            int i = 32;
            char[] buffer = new char[i];
            int targetBase = baseChars.Length;

            do
            {
                buffer[--i] = baseChars[ticks % targetBase];
                ticks = ticks / targetBase;
            }
            while (ticks > 0);

            char[] result = new char[32 - i];
            Array.Copy(buffer, i, result, 0, 32 - i);

            return string.Format("{0}{1}", !string.IsNullOrEmpty(BranchID) ? BranchID.ToString() + "-" : string.Empty, new string(result));
        }

        public static void AddProperty(ExpandoObject expando, string propertyName, object propertyValue)
        {
            // ExpandoObject supports IDictionary so we can extend it like this
            var expandoDict = expando as IDictionary<string, object>;
            if (expandoDict.ContainsKey(propertyName))
                expandoDict[propertyName] = propertyValue;
            else
                expandoDict.Add(propertyName, propertyValue);
        }

        public static dynamic GetDynamicObject(Dictionary<string, object> properties)
        {
            return new MyDynObject(properties);
        }

        public sealed class MyDynObject : DynamicObject
        {
            private readonly Dictionary<string, object> _properties;

            public MyDynObject(Dictionary<string, object> properties)
            {
                _properties = properties;
            }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                return _properties.Keys;
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                if (_properties.ContainsKey(binder.Name))
                {
                    result = _properties[binder.Name];
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            public override bool TrySetMember(SetMemberBinder binder, object value)
            {
                if (_properties.ContainsKey(binder.Name))
                {
                    _properties[binder.Name] = value;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets a generic objects casted name.
        /// </summary>
        /// <param name="item">DataTable or DataSet</param>
        /// <returns>DataTable.TableName or DataSet.DataSetName</returns>
        public static string GetObjectName(this object item)
        {
            if (item == null) return string.Empty;

            var ret = string.Empty;

            var dt = item as DataTable;
            if (dt != null)
            {
                ret = dt.TableName;
            }

            var ds = item as DataSet;
            if (ds != null)
            {
                ret = ds.DataSetName;
            }

            return ret;
        }


        public static string ConvertToAlpha(this int index)
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var value = "";

            if (index >= letters.Length)
                value += letters[index / letters.Length - 1];

            value += letters[index % letters.Length];

            return value;
        }

        public static IDictionary<string, object> ToDictionary(this object values)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (values != null)
            {
                foreach (PropertyDescriptor propertyDescriptor in TypeDescriptor.GetProperties(values))
                {
                    object obj = propertyDescriptor.GetValue(values);
                    dict.Add(propertyDescriptor.Name, obj);
                }
            }

            return dict;
        }

        public static object FromDictToAnonymousObj<TValue>(IDictionary<string, TValue> dict)
        {
            var types = new Type[dict.Count];

            for (int i = 0; i < types.Length; i++)
            {
                types[i] = typeof(TValue);
            }

            // dictionaries don't have an order, so we force an order based
            // on the Key
            var ordered = dict.ToArray(); //dict.OrderBy(x => x.Key).ToArray();

            string[] names = Array.ConvertAll(ordered, x => x.Key);

            Type type = AnonymousType.CreateType(types, names);

            object[] values = Array.ConvertAll(ordered, x => (object)x.Value);

            object obj = type.GetConstructor(types).Invoke(values);

            return obj;
        }

    }
}
