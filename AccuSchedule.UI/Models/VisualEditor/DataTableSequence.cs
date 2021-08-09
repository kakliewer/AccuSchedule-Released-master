using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Models.VisualEditor
{
    public class DataTableSequence : DataTableValue
    {
        public List<DataTableValue> Tables { get; } = new List<DataTableValue>();

        public DataTableSequence()
        { }

        public DataTableSequence(IEnumerable<DataTableValue> tables) => Tables.AddRange(tables);

        public string Compile(CompilerContext context)
        {
            string result = "";
            foreach (DataTableValue table in Tables)
            {
                result += table.Compile(context);
                result += "\n";
            }
            return result;
        }
    }
}
