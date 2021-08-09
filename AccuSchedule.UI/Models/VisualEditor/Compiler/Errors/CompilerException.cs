using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor.Compiler.Errors
{
    public class CompilerException : Exception
    {
        public CompilerException(string msg) : base(msg)
        { }

        public CompilerException(string msg, Exception inner) : base(msg, inner)
        { }
    }
}
