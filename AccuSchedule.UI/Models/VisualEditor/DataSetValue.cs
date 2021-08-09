using AccuSchedule.UI.Models.VisualEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    [DataContract]
    public class DataSetValue : ITypedExpression<DataSet>
    {
        [DataMember]
        public DataSet Value { get; set; }

        public string Compile(CompilerContext ctx)
        {
            return Value?.DataSetName;
        }
    }
}
