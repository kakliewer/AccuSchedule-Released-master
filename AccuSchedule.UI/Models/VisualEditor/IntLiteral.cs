using AccuSchedule.UI.Models.VisualEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    public class IntLiteral : ITypedExpression<int>
    {
        public int Value { get; set; }

        public string Compile(CompilerContext context)
        {
            return Value.ToString();
        }
    }
}
