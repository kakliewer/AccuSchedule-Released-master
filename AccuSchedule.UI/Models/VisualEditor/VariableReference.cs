using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.Models.VisualEditor.Compiler.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    public class VariableReference<T> : ITypedExpression<T>
    {
        public ITypedVariableDefinition<T> LocalVariable { get; set; }

        public string Compile(CompilerContext context)
        {
            if (!context.IsInScope(LocalVariable))
            {
                throw new VariableOutOfScopeException(LocalVariable.VariableName);
            }
            return LocalVariable.VariableName;
        }
    }
}
