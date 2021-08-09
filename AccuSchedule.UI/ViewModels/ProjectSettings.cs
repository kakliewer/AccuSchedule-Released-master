using AccuSchedule.UI.Models;
using AccuSchedule.UI.ViewModels.VisualEditor;
using AccuSchedule.UI.Views;
using AccuSchedule.UI.Views.VisualEditor;
using DocumentFormat.OpenXml.Drawing.Charts;
using NodeNetwork.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.IO;

namespace AccuSchedule.UI.ViewModels
{
    [Serializable]
    public class ProjectSettings
    {
        public string Name { get; set; }
        public string LastSaveDirectory { get; set; }

        public IEnumerable<ProjectNode> Nodes { get; set; }

        
    }

    [Serializable]
    public class ProjectNode
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string NodeType { get; set; }
        public string FileName { get; set; }
        public Point Position { get; set; }
        public string Category { get; set; }
        public Type ToolClass { get; set; }
        public string ToolName { get; set; }
        public string XMLPayLoad { get; set; }
        public IEnumerable<int> Connections { get; set; }
        public Dictionary<string, ParamTab> Props { get; set; }

        // Used for identifing other properties for serialization
        [field: NonSerialized]
        public DefaultNodeViewModel _Node { get; set; }
        [field: NonSerialized]
        public IEnumerable<DefaultNodeViewModel> _NodeConnections { get; set; }
    }
}
