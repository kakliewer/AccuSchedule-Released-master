using AccuSchedule.UI.Models;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    public interface IVariableDefinition : IStatement
    {
        string VariableName { get; }
    }

    public interface ITypedVariableDefinition<T> : IVariableDefinition
    {

    }
}
