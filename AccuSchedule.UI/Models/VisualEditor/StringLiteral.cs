using AccuSchedule.UI.Models.VisualEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    public class StringLiteral : ITypedExpression<string>
    {
        public string Value { get; set; }

        public string Compile(CompilerContext ctx)
        {
            return $"\"{Value}\"";
        }
    }
}
