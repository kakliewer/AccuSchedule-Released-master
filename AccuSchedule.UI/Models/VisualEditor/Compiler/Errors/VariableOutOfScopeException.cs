using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor.Compiler.Errors
{
    public class VariableOutOfScopeException : CompilerException
    {
        public string VariableName { get; }

        public VariableOutOfScopeException(string variableName)
            : base($"The variable '{variableName}' was referenced outside its scope.")
        {
            VariableName = variableName;
        }
    }
}
