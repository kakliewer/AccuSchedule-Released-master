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
    public class DataTableValue : ITypedExpression<DataTable>
    {
        [DataMember]
        public DataTable Value { get; set; }

        public string Compile(CompilerContext ctx)
        {
            return Value?.TableName;
        }
    }
}
