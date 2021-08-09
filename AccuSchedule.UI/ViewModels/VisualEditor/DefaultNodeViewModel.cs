using AccuSchedule.UI.Models;
using AccuSchedule.UI.Views;
using AccuSchedule.UI.Views.VisualEditor;
using NodeNetwork.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
    [Serializable]
    public class DefaultNodeViewModel : NodeViewModel
    {
        public enum NodeType
        {
            EventNode, Function, FlowControl, Literal, TableProcessing, DataTable, DataSet, DataSetFromTable, DataTableFromSet, Void, ObjList
        }

        [DataMember]
        public string Category { get; set; }

        static DefaultNodeViewModel() => Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<DefaultNodeViewModel>));

        public NodeType TypeOfNode { get; }

        [DataMember]
        public Dictionary<string, ParamTab> Properties { get; set; } = new Dictionary<string, ParamTab>();

        public DefaultNodeViewModel(NodeType type) => TypeOfNode = type;

    }
}
