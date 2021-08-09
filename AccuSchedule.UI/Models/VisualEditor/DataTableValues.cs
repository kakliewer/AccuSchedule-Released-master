using AccuSchedule.UI.Models.VisualEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    public class DataTableValues : IStatement
    {
        public IStatement Input { get; set; }

        public string Compile(CompilerContext context)
        {
            context.EnterNewScope("For loop");

            //CurrentIndex.Value = LowerBound;
            //string code = $"for {CurrentIndex.Compile(context)}, {UpperBound.Compile(context)} do\n" +
             //      LoopBody.Compile(context) + "\n" +
            //       $"end\n";

            context.LeaveScope();
            return string.Empty;
        }
    }
}
