using AccuSchedule.UI.Extensions;
using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models;
using AccuSchedule.UI.ViewModels.VisualEditor;
using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
using AccuSchedule.UI.Views.VisualEditor;
using MahApps.Metro.Controls;
using Ninject;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AccuSchedule.UI.ViewModels
{
    public class ToolProcessor : ToolsProcessorBase
    {

        public delegate void UpdateEvent(ViewTabs item);
        public static event UpdateEvent OnUpdate;

        public delegate void Execution();
        public static event Execution IsExecuting;
        public static event Execution DoneExecuting;

        public MainWindow Host { get; set; }

        public override Type[] AllowedTypes =>
            new Type[] { }; // Only allow these types in the view

        public ToolProcessor(MainWindow host)
        {
            Host = host;
        }

        public void AddToolNodes(DefaultNodeListViewModel nodeList, IToolPlugin ToolsClass, Type ReturnTypeRestriction = null)
        {
            // Get Section Name from Class
            var sectionName = GetSectionName(ToolsClass, ReturnTypeRestriction);
            
            // Get the functions from the class
            var functions = GetToolsClassItems(ToolsClass, ReturnTypeRestriction);

            // Gets any pre-loaded Static objects to load into the nodes for custom processing
            var staticMembers = GetToolsStaticReadOnlyItems(ToolsClass);

            // Check if it's already in the nodelist
            foreach (var function in functions)
            {
                if (!nodeList.Nodes.Items.Any(a => a.Name == function.ToolMethodInfo.Name))
                {

                    //? ##############################
                    //! Determine Type of Node to load
                    //? ##############################
                    if (function.ToolMethodInfo.ReturnType == typeof(DataSet) && function.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(DataTable)))
                    {
                        nodeList.AddNodeType(() =>
                            new DataSetFromTableProcessingNode(sectionName, Host)
                            {
                                Name = function.ToolMethodInfo.Name,
                                Tool = function,
                                InjectedObjects = staticMembers
                            }
                        );
                    }
                    else if (function.ToolMethodInfo.ReturnType == typeof(DataTable) && function.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(DataSet)))
                    {
                        nodeList.AddNodeType(() =>
                            new DataTableFromSetProcessingNode(sectionName, Host)
                            {
                                Name = function.ToolMethodInfo.Name,
                                Tool = function,
                                InjectedObjects = staticMembers
                            }
                        );
                    }
                    else if (function.ToolMethodInfo.ReturnType == typeof(DataSet) && !function.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(DataTable))
                        && function.ToolMethodInfo.GetParameters().First().ParameterType != typeof(IEnumerable<object>))
                    {
                        nodeList.AddNodeType(() =>
                            new DataSetProcessingNode(sectionName, Host)
                            {
                                Name = function.ToolMethodInfo.Name,
                                Tool = function,
                                InjectedObjects = staticMembers
                            }
                        );
                    }
                    else if (function.ToolMethodInfo.ReturnType == typeof(DataTable) && !function.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(DataSet)))
                    {
                        nodeList.AddNodeType(() =>
                            new DataTableProcessingNode(Host, sectionName)
                            {
                                Name = function.ToolMethodInfo.Name,
                                Tool = function,
                                InjectedObjects = staticMembers
                            }
                        );
                    }
                    else if (function.ToolMethodInfo.ReturnType == typeof(void) && function.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(IEnumerable<object>)))
                    {
                        nodeList.AddNodeType(() =>
                            new ExportNode(sectionName)
                            {
                                Name = function.ToolMethodInfo.Name,
                                Tool = function,
                                InjectedObjects = staticMembers
                            }
                        );
                    }
                    else if (function.ToolMethodInfo.ReturnType == typeof(DataSet) && function.ToolMethodInfo.GetParameters().First().ParameterType == typeof(IEnumerable<object>))
                    {
                        nodeList.AddNodeType(() =>
                            new ObjListToDataSetNode(sectionName, Host)
                            {
                                Name = function.ToolMethodInfo.Name,
                                Tool = function,
                                InjectedObjects = staticMembers
                            }
                        );
                    }





                }
            }

        }
        public (ToolID Tool, List<System.Reflection.MemberInfo> Injections) GetToolOfNode(string Toolname, Type Tool)
        {

            IToolPlugin ToolsClass;

            // Load the Tool Plugins through Ninject's kernel
            var theApp = Application.Current as App;
            var availTools = theApp.kernel.GetAll<IToolPlugin>();

            var matchedTool = availTools.Where(w => w.GetType() == Tool).FirstOrDefault();

            if (matchedTool != null)
            {
                ToolsClass = matchedTool;
                // Get Section Name from Class
                var sectionName = GetSectionName(ToolsClass);

                // Gets any pre-loaded Static objects to load into the nodes for custom processing
                var staticMembers = GetToolsStaticReadOnlyItems(ToolsClass);

                // Get the functions from the class
                var functions = GetToolsClassItems(ToolsClass, null);


                return (functions.Where(w => w.ToolMethodInfo.Name == Toolname).FirstOrDefault(), staticMembers);
            }

            return (null, null);
        }

        public string GetSectionName(IToolPlugin ToolsClass, Type ReturnTypeRestriction = null)
        {
            var sectionName = string.Empty;
            if (ReturnTypeRestriction == null)
            {
                sectionName = ToolsClass.DefaultSection;
            }
            else
            {
                var classSection = ToolsClass.NameOfSection(ReturnTypeRestriction);
                if (!string.IsNullOrEmpty(classSection))
                {
                    sectionName = classSection;
                }
            }

            return sectionName;
        }

        public object Executioner(ToolID method, string toExecute)
        {
            // Make sure parameters are valid
            if (string.IsNullOrEmpty(toExecute) && method.ToolMethodInfo.GetParameters().Count() > 1) return null;

            // Create instance of class
            object obj = Activator.CreateInstance(method.ToolType);

            // Execute the method
            var command = this.Engine()
                .SetValue("func", obj)
                .Execute(@"func." + method.ToolMethodInfo.Name + @"(" + toExecute + ");")
                .GetCompletionValue()
                .ToObject();

            return command;
        }
        public object Executioner(ToolID method, DataTable toExecute, string parameters = null)
        {
            // Make sure parameters are valid
            if (string.IsNullOrEmpty(parameters) && method.ToolMethodInfo.GetParameters().Count() > 2) return null;

            // Create instance of class
            object obj = Activator.CreateInstance(method.ToolType);

            // Execute the method
            object command = null;

            if (method.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(Action<DataTable>) || a.ParameterType == typeof(Action<DataSet>)))
            { // Add the null for the action
                if (string.IsNullOrEmpty(parameters))
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute, null);")
                    .GetCompletionValue()
                    .ToObject();
                }
                else
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute, null, " + parameters + ");")
                    .GetCompletionValue()
                    .ToObject();
                }
            }
            else
            {
                command = this.Engine()
                .SetValue("func", obj)
                .SetValue("toExecute", toExecute)
                .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute);")
                .GetCompletionValue()
                .ToObject();
            }

            var vt = new ViewTabs() { Table = toExecute, Set = command.GetType() == typeof(DataSet) ? command as DataSet : null };

            OnUpdate(vt);
            return command;
        }
        public object Executioner(ToolID method, DataSet toExecute, string parameters = null)
        {
            // Make sure parameters are valid
            if (string.IsNullOrEmpty(parameters) && method.ToolMethodInfo.GetParameters().Count() > 2) return null;

            // Create instance of class
            object obj = Activator.CreateInstance(method.ToolType);

            // Execute the method
            object command = null;

            if (method.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(Action<DataTable>) || a.ParameterType == typeof(Action<DataSet>)))
            { // Add the null for the action
                if (string.IsNullOrEmpty(parameters))
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute, null);")
                    .GetCompletionValue()
                    .ToObject();
                } else
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute, null, " + parameters + ");")
                    .GetCompletionValue()
                    .ToObject();
                }
            }
            else
            { // Process as normal
                command = this.Engine()
                .SetValue("func", obj)
                .SetValue("toExecute", toExecute)
                .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute);")
                .GetCompletionValue()
                .ToObject();
            }

            var retVT = new ViewTabs();
            if (command?.GetType() == typeof(DataTable))
            {
                retVT.Set = toExecute;
                retVT.Table = command as DataTable;
            }
                
            if (command?.GetType() == typeof(DataSet))
            {
                retVT.Set = command as DataSet;
                retVT.Table = null;
            }

            OnUpdate(retVT);
            return command;
        }
        public object ExecutionerObjList(ToolID method, IEnumerable<object> toExecute, string parameters = null)
        {
            // Make sure parameters are valid
            if (string.IsNullOrEmpty(parameters) && method.ToolMethodInfo.GetParameters().Count() > 2) return null;

            // Create instance of class
            object obj = Activator.CreateInstance(method.ToolType);

            // Execute the method
            object command = null;

            if (method.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(Action<IEnumerable<object>>)))
            { // Add the null for the action
                if (string.IsNullOrEmpty(parameters))
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute, null);")
                    .GetCompletionValue()
                    .ToObject();
                }
                else
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute, null, " + parameters + ");")
                    .GetCompletionValue()
                    .ToObject();
                }
            }
            else
            { // Process as normal
                command = this.Engine()
                .SetValue("func", obj)
                .SetValue("toExecute", toExecute)
                .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute);")
                .GetCompletionValue()
                .ToObject();
            }


            var retVT = new ViewTabs();
            if (command?.GetType() == typeof(DataTable))
            {
                retVT.Set = null;
                retVT.Table = command as DataTable;
            }

            if (command?.GetType() == typeof(DataSet))
            {
                retVT.Set = command as DataSet;
                retVT.Table = null;
            }

            OnUpdate(retVT);
            return command;
        }

        public void Executioner(ToolID method, IEnumerable<object> toExecute, string parameters = null)
        {
            // Make sure parameters are valid
            if (string.IsNullOrEmpty(parameters) && method.ToolMethodInfo.GetParameters().Count() > 2) return;

            // Create instance of class
            object obj = Activator.CreateInstance(method.ToolType);

            // Execute the method
            object command = null;

            if (method.ToolMethodInfo.GetParameters().Any(a => a.ParameterType == typeof(Action<DataTable>) || a.ParameterType == typeof(Action<DataSet>)))
            { // Add the null for the action
                if (string.IsNullOrEmpty(parameters))
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(null, toExecute);")
                    .GetCompletionValue()
                    .ToObject();
                }
                else
                {
                    command = this.Engine()
                    .SetValue("func", obj)
                    .SetValue("toExecute", toExecute)
                    .Execute(@"func." + method.ToolMethodInfo.Name + @"(null, toExecute, " + parameters + ");")
                    .GetCompletionValue()
                    .ToObject();
                }
            }
            else
            {
                command = this.Engine()
                .SetValue("func", obj)
                .SetValue("toExecute", toExecute)
                .Execute(@"func." + method.ToolMethodInfo.Name + @"(toExecute);")
                .GetCompletionValue()
                .ToObject();
            }

        }


        public override void PropActionButton_Click(object sender, RoutedEventArgs e)
        {
            IsExecuting();
            var btn = sender as Button;

            var parent = btn.Parent as StackPanel;
            var host = parent.GetAncestorOfType<StackPanel>();
            var targets = new List<object>();

            // Loop through the children in order and construct return parameters
            foreach (var child in host.Children)
            {
                var panel = child as StackPanel;

                var txt = panel.FindChildren<TextBox>().FirstOrDefault();
                var cmbo = panel.FindChildren<ComboBox>().FirstOrDefault();
                var executeBtn = panel.FindChildren<Button>().FirstOrDefault();

                if (txt != null) targets.Add(txt);
                if (cmbo != null) targets.Add(cmbo);
                if (executeBtn != null && executeBtn.Tag?.ToString() == txt?.Name
                    || executeBtn != null && executeBtn.Tag?.ToString() == cmbo?.Name) 
                    targets.Add(null);
            }


            if (btn != null)
            {
                var strParams = ProcessMethodReturnParameters(targets);

                var tool = btn.Tag as ParamTab;
                try
                {
                    if (!tool.Tab.isVoid && tool.Tab.Table != null && tool.Tab.Set == null && tool.Tab.getTable == true && tool.Tab.getSet == false)
                        tool.Tab.Table = Executioner(tool.Tab.ToolMethod, tool.Tab.Table, strParams) as DataTable;
                    else if (!tool.Tab.isVoid && tool.Tab.Table == null && tool.Tab.Set != null && tool.Tab.getTable == false && tool.Tab.getSet == true)
                        tool.Tab.Set = Executioner(tool.Tab.ToolMethod, tool.Tab.Set, strParams) as DataSet;

                    else if (!tool.Tab.isVoid && tool.Tab.Table != null && tool.Tab.Set != null && tool.Tab.getTable == false && tool.Tab.getSet == true)
                        tool.Tab.Table = Executioner(tool.Tab.ToolMethod, tool.Tab.Table, strParams) as DataTable;
                    else if (!tool.Tab.isVoid && tool.Tab.Table != null && tool.Tab.Set != null && tool.Tab.getTable == true && tool.Tab.getSet == false)
                        tool.Tab.Set = Executioner(tool.Tab.ToolMethod, tool.Tab.Set, strParams) as DataSet;

                    else if (!tool.Tab.isVoid && tool.Tab.Table == null && tool.Tab.Set == null && tool.Tab.getTable == true && tool.Tab.getSet == false)
                        tool.Tab.Table = Executioner(tool.Tab.ToolMethod, tool.Tab.Table, strParams) as DataTable;
                    else if (tool.Tab.Table == null && tool.Tab.Set == null && tool.Tab.getTable == false && tool.Tab.getSet == true && tool.Tab.ObjPayload == null)
                        tool.Tab.Set = Executioner(tool.Tab.ToolMethod, tool.Tab.Set, strParams) as DataSet;

                    else if (!tool.Tab.isVoid && tool.Tab.Table != null && tool.Tab.Set == null && tool.Tab.getTable == false && tool.Tab.getSet == false)
                        tool.Tab.Table = Executioner(tool.Tab.ToolMethod, tool.Tab.Table, strParams) as DataTable;
                    else if (!tool.Tab.isVoid && tool.Tab.Table == null && tool.Tab.Set != null && tool.Tab.getTable == false && tool.Tab.getSet == false)
                        tool.Tab.Set = Executioner(tool.Tab.ToolMethod, tool.Tab.Set, strParams) as DataSet;

                    else if (tool.Tab.isVoid)
                        Executioner(tool.Tab.ToolMethod, tool.Tab.ObjPayload, strParams);
                    else if (tool.Tab.getObjects)
                        tool.Tab.Set = ExecutionerObjList(tool.Tab.ToolMethod, tool.Tab.ObjPayload, strParams) as DataSet;
                }
                catch(Exception err) 
                {
                    Host.scope.UpdateError(tool, err);

                    
                }
                // Populate the ValueEditor of the node
                PopulateValueEditorOfNode(tool);
                DoneExecuting();
            }
        }

        public void PopulateValueEditorOfNode(ParamTab tool)
        {

            var setToTableNode = tool.Tab.Node as DataTableFromSetProcessingNode;
            if (setToTableNode != null && setToTableNode.ValueEditor != null) { setToTableNode.ValueEditor.Value = tool.Tab.Table; return; }

            var tableToSetNode = tool.Tab.Node as DataSetFromTableProcessingNode;
            if (tableToSetNode != null && tableToSetNode.ValueEditor != null) { tableToSetNode.ValueEditor.Value = tool.Tab.Set; return; }

            var SetNode = tool.Tab.Node as DataSetProcessingNode;
            if (SetNode != null && SetNode.ValueEditor != null) { SetNode.ValueEditor.Value = tool.Tab.Set; return; }

            var TableNode = tool.Tab.Node as DataTableProcessingNode;
            if (TableNode != null && TableNode.ValueEditor != null) { TableNode.ValueEditor.Value = tool.Tab.Table; return; }

            var objSetNode = tool.Tab.Node as ObjListToDataSetNode;
            if (objSetNode != null && objSetNode.ValueEditor != null) { objSetNode.ValueEditor.Value = tool.Tab.Set; return; }


        }

        public override void PropFileBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var parent = btn.Parent as StackPanel;
                var txtChildren = parent.Children.OfType<TextBox>();
                if (txtChildren.Any())
                {
                    // Got the browse button click, Tag should indicate textbox name for match.
                    var txt = txtChildren.FirstOrDefault();
                    txt.Text = FileExtensions.OpenFileOrNull(DefaultExtension: "xlsx", Filter: @"Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*")?.FileName;
                    txt.Focus();
                    txt.CaretIndex = txt.Text.Length;
                }
                
            }
        }

        public override void PropDirBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var parent = btn.Parent as StackPanel;
                var txtChildren = parent.Children.OfType<TextBox>();
                if (txtChildren.Any())
                {
                    // Got the browse button click, Tag should indicate textbox name for match.
                    var txt = txtChildren.FirstOrDefault();
                    txt.Text = FileExtensions.OpenDirOrNull()?.SelectedPath;
                    txt.Focus();
                    txt.CaretIndex = txt.Text.Length;
                }

            }
        }
    }
}
