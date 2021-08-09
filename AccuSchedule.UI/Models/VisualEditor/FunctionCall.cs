using AccuSchedule.UI.Models.VisualEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    public class FunctionCall : DataTableValue
    {
        public string FunctionName { get; set; }
        public List<IExpression> Parameters { get; } = new List<IExpression>();

        public string Compile(CompilerContext context)
        {
            if (context.VariablesScopesStack.Count > 0)
                return $"{FunctionName}({String.Join(", ", Parameters.Select(p => p.Compile(context)))})\n";
            else
                return $"{FunctionName}()\n";
        }
    }
}
