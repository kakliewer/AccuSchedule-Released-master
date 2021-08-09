using Jint;
using Jint.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Methods
{
    public class Tools
    {
        public Engine Engine() => new Engine(cfg => cfg.AllowClr());

        
    }
}
