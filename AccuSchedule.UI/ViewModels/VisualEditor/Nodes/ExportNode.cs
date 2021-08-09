using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models;
using AccuSchedule.UI.Models.VisualEditor;
using AccuSchedule.UI.Models.VisualEditor.Compiler;
using AccuSchedule.UI.ViewModels.VisualEditor.Editors;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
using NodeNetwork.Toolkit.ValueNode;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static AccuSchedule.UI.ViewModels.VisualEditor.DefaultPortViewModel;

namespace AccuSchedule.UI.ViewModels.VisualEditor.Nodes
{
    [Serializable]
    public class ExportNode : DefaultNodeViewModel, INodeProperties
    {

        static ExportNode() => Splat.Locator.CurrentMutable.Register(() => new DefaultNodeView(), typeof(IViewFor<ExportNode>));

        public DefaultOutputViewModel<DataTableValue> dtConnectionFlow { get; }
        public DefaultOutputViewModel<DataSetValue> dsConnectionFlow { get; }


        public VoidEditorViewModel ValueEditor { get; set; }

        // INodeProperties
        public ToolID Tool { get; set; }
        public List<MemberInfo> InjectedObjects { get; set; }

        public ExportNode(string category = "", string title = "Export") : base(NodeType.Void)
        {
            this.Category = category;
            this.Name = title;

            dtConnectionFlow = new DefaultOutputViewModel<DataTableValue>(PortType.DataTable)
            {
                Name = string.Format("")
            };
            this.Outputs.Add(dtConnectionFlow);

            dsConnectionFlow = new DefaultOutputViewModel<DataSetValue>(PortType.DataSet)
            {
                Name = string.Format("")
            };
            this.Outputs.Add(dsConnectionFlow);
        }
    }
}
