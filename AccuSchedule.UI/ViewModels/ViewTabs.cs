using AccuSchedule.UI.Models;
using AccuSchedule.UI.ViewModels.VisualEditor;
using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
using AccuSchedule.UI.Views;
using AccuSchedule.UI.Views.VisualEditor;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AccuSchedule.UI.ViewModels
{
    [Serializable]
    public class ViewTabs
    {
        public string Header { get; set; }

        public Dictionary<string, ParamTab> Props { get; set; } = new Dictionary<string, ParamTab>();


        public object Node { get; set; }


        public ParameterInfo ParamInfo { get; set; }


        public DataSet Set { get; set; }

        public bool getSet { get; set; } = false;

    
        public DataTable Table { get; set; }
 
        public bool getTable { get; set; } = false;

   
        public DataView dataView { get => Table?.DefaultView; }

       
        public ToolID ToolMethod { get; set; }

        public bool isVoid { get; set; } = false;

    
        public IEnumerable<object> ObjPayload { get; set; }
        public bool getObjects { get; set; } = false;

        public List<MemberInfo> InjectedObjects { get; set; }

        
        public Dictionary<DefaultNodeViewModel, IEnumerable<object>> NodeChain { get; set; }

        public void NewTable(DataTable table)
        {
            Table = table;
        }

    }
}