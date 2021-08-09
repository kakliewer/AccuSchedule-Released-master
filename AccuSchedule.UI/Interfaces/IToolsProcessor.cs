using AccuSchedule.UI.Models;
using AccuSchedule.UI.ViewModels;
using Jint;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AccuSchedule.UI.Interfaces;
using System.Text.RegularExpressions;
using MaterialDesignThemes.Wpf;
using AccuSchedule.UI.Extensions;
using System.Collections;
using System.Web;
using DataTableExtensions = AccuSchedule.UI.Extensions.DataTableExtensions;
using System.IO;

namespace AccuSchedule.UI.Interfaces
{
    interface IToolsProcessor
    {

    }

    public abstract class ToolsProcessorBase : IToolsProcessor
    {
        public abstract Type[] AllowedTypes { get; }

        public virtual List<ToolID> GetToolsClassItems(object SectionTools, Type ReturnTypeRestriction)
        {
            // Add each public method in the class
            List<ToolID> retTools = new List<ToolID>();

            SectionTools.GetType()
                .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .ForEach(method =>
                {
                    method = method as MethodInfo;

                    bool addIt = false;
                    if (method.Name == "get_Name"
                    || method.Name == "NameOfSection"
                    || method.Name == "get_TypesToLoad"
                    || AllowedTypes.Any() && !AllowedTypes.Any(t => method.ReturnType == t))
                    {
                        addIt = false;
                    }
                    else if (ReturnTypeRestriction != null && method.ReturnType == ReturnTypeRestriction)
                    {
                        addIt = true;
                    }
                    else
                    {
                        if (ReturnTypeRestriction == null) addIt = true;
                    }

                    if (addIt)
                    {
                        retTools.Add(new ToolID() { ToolType = SectionTools.GetType(), ToolMethodInfo = method });
                    }
                });

            return retTools;

        }

        public virtual bool IsToolPayloadSavable(object SectionTools)
        {
            var ret = true;
            var toolID = SectionTools as ToolID;
            if (toolID != null)
            {
                var method = toolID.ToolMethodInfo.DeclaringType
                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Cast<MethodInfo>()
                    .Where(w => w.Name == "get_PayLoadIsSavable")
                    .FirstOrDefault();

                if (method != null)
                {
                    ret = Convert.ToBoolean(method.Invoke(null, null));
                }
                    

            }
            return ret;

        }

        public virtual List<MemberInfo> GetToolsStaticReadOnlyItems(object SectionTools)
        {
            // Add each public method in the class
            List<MemberInfo> retMembers = new List<MemberInfo>();

            SectionTools.GetType()
                .GetMembers(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .ForEach(member =>
                {
                    member = member as MemberInfo;
                    if (member != null)
                    {
                        bool addIt = false;
                        if (member.Name == "title"
                        || member.Name == "Finalize"
                        || member.Name == "MemberwiseClone"
                        || member.Name == ".cctor")
                        {
                            addIt = false;
                        }
                        else
                        {
                            addIt = true;
                        }

                        if (addIt)
                        {
                            retMembers.Add(member);
                        }
                    }
                });

            return retMembers;

        }

        public static string CleanInput(string strIn)
        {
            // Replace invalid characters with empty strings.
            try
            {
                return Regex.Replace(strIn, @"[^\w]", "",
                                     RegexOptions.None, TimeSpan.FromSeconds(1.5));
            }
            // If we timeout when replacing invalid characters,
            // we should return Empty.
            catch (RegexMatchTimeoutException)
            {
                return String.Empty;
            }
        }

        public virtual List<UIElement> ProcessMethodInputParameters(ViewTabs tab)
        {
            if (tab.ToolMethod?.ToolMethodInfo == null) return null;

            var method = tab.ToolMethod.ToolMethodInfo;

            List<UIElement> ret = new List<UIElement>();

            // Check Parameters
            foreach (var param in method.GetParameters())
            {
                
                var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };

                if (param.ParameterType == typeof(string) || param.ParameterType == typeof(int) || param.ParameterType == typeof(double))
                {
                    if (param.Name.StartsWith("_Combo_"))
                    { // Process As ComboBox
                        foreach (var item in ProcessComboBox(param, tab))
                            stackPanel.Children.Add(item);

                        ret.Add(stackPanel);
                    }
                    else
                    { // Process as normal Textbox
                        foreach (var item in ProcessTextBox(param, tab))
                            stackPanel.Children.Add(item);

                        ret.Add(stackPanel);
                    }
                }
                else if (param.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<string>))
                    || param.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<int>))
                    || param.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<double>)))
                { // Process as mtuli-line textbox to accept Arrays
                    foreach (var item in ProcessArray(param, tab))
                        stackPanel.Children.Add(item);

                    ret.Add(stackPanel);
                }
                else if (param.ParameterType == typeof(Action<DataSet>) || param.ParameterType == typeof(Action<DataTable>) || param.ParameterType == typeof(Action<IEnumerable<object>>))
                {
                    foreach (var item in ProcessActionData(param, tab))
                        stackPanel.Children.Add(item);

                    ret.Add(stackPanel);
                }
                else if (param.ParameterType == typeof(Button))
                    ProcessButton(param, tab, ret.LastOrDefault() as StackPanel);
                else if (param.ParameterType == typeof(IEnumerable<object>))
                    ret.Add(ProcessVoid(param, tab));

            }


            return ret;
        }

        protected virtual HashSet<UIElement> ProcessComboBox(ParameterInfo param, ViewTabs tab)
        {
            var ret = new HashSet<UIElement>();

            var paramTab = new ParamTab() { Tab = tab, ParamInfo = param };

            var paramSplitter = param.Name.Split('_');
            var lblText = paramSplitter[2];
            var itemsListName = paramSplitter[3];

            // Make sure there is a static object to load the items into the combobox
            var staticMember = tab.InjectedObjects.Where(w => w.Name.Equals(itemsListName)).FirstOrDefault();
            if (staticMember != null)
            {
                var name = new Label() { Content = string.Format("{0} ({1})", lblText, param.ParameterType.Name) };
                var cmbo = new ComboBox()
                {
                    Name = lblText.ToString(),
                    Tag = paramTab,
                    IsEditable = true,
                    VerticalAlignment = VerticalAlignment.Top,
                    ItemsSource = staticMember.GetMemberValue() as IEnumerable<object>
                };
                ComboBoxAssist.SetShowSelectedItem(cmbo, true);
                HintAssist.SetHint(cmbo, "Select method.");

                ret.Add(name);
                ret.Add(cmbo);
            }

            return ret;
        }
        protected virtual HashSet<UIElement> ProcessTextBox(ParameterInfo param, ViewTabs tab)
        {
            var ret = new HashSet<UIElement>();

            var paramTab = new ParamTab() { Tab = tab, ParamInfo = param };

            var name = new Label() { Content = string.Format("{0} ({1})", param.Name, param.ParameterType.Name) };
            var value = new TextBox() { Tag = paramTab, Name = param.Name, TextWrapping = TextWrapping.Wrap, MinWidth = 100, AcceptsReturn = false };

            ret.Add(name);
            ret.Add(value);

            return ret;
        }
        protected virtual HashSet<UIElement> ProcessArray(ParameterInfo param, ViewTabs tab)
        {
            var ret = new HashSet<UIElement>();

            var paramTab = new ParamTab() { Tab = tab, ParamInfo = param };

            var name = new Label() { Content = string.Format("{0} ({1})", param.Name, param.ParameterType.Name) };
            var value = new TextBox() { Tag = paramTab, Name = param.Name, TextWrapping = TextWrapping.Wrap, MinWidth = 100, AcceptsReturn = true };

            ret.Add(name);
            ret.Add(value);

            return ret;
        }
        protected virtual HashSet<UIElement> ProcessActionData(ParameterInfo param, ViewTabs tab)
        {
            var ret = new HashSet<UIElement>();

            var paramTab = new ParamTab() { Tab = tab, ParamInfo = param };

            var btn = new Button() { Tag = paramTab, Content = param.Name };
            btn.Click += PropActionButton_Click;

            ret.Add(btn);

            return ret;
        }
        protected virtual void ProcessButton(ParameterInfo param, ViewTabs tab, StackPanel lastStack)
        {
            var paramTab = new ParamTab() { Tab = tab, ParamInfo = param };

            // If a "Browse" string found in name then use the Browsing functionality and return string to appropriate TextBox.
            if (param.Name.ToLower().StartsWith("_browse"))
            { // Get the last Stack Panel and add it to that
                var btn = new Button()
                {
                    Content = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.DotsHorizontal },
                    Margin = new Thickness(5, 0, 0, 2),
                    Style = (Style)Application.Current.TryFindResource("MaterialDesignFloatingActionMiniButton"),
                    Height = 30,
                    Width = 30
                };

                if (param.Name.ToLower().StartsWith("_browseforfile")) btn.Click += PropFileBrowseButton_Click;
                else if (param.Name.ToLower().StartsWith("_browsefordir")) btn.Click += PropDirBrowseButton_Click;

                if (lastStack != null)
                {
                    var txt = lastStack.Children.OfType<TextBox>();
                    if (txt.Any())
                    {
                        var asTxt = txt.FirstOrDefault() as TextBox;
                        if (asTxt != null)
                        {
                            btn.Tag = asTxt.Name;
                            lastStack.Children.Add(btn);
                        }
                    }
                }
            }


        }
        protected virtual UIElement ProcessVoid(ParameterInfo param, ViewTabs tab)
        {
            var paramTab = new ParamTab() { Tab = tab, ParamInfo = param };

            // If a "Browse" string found in name then use the Browsing functionality and return string to appropriate TextBox.
            var sp = new StackPanel();
            var itemsLbl = new TextBlock() { Text = "Objects Being Exported:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 3) };
            sp.Children.Add(itemsLbl);

            if (tab.ObjPayload != null)
            {
                
                var txt = new TextBlock()
                {
                    Text = string.Join(System.Environment.NewLine
                    , tab.ObjPayload.Select((s, idx) => string.Format("{2}.) \"{0}\" ({1})", s.GetObjectName(), s.GetType().Name, idx + 1)))
                    ,
                    Margin = new Thickness(0, 0, 0, 10)
                    ,
                    Tag = paramTab
                };
                
                sp.Children.Add(txt);
            }

            return sp;

        }


        public abstract void PropActionButton_Click(object sender, RoutedEventArgs e);
        public abstract void PropFileBrowseButton_Click(object sender, RoutedEventArgs e);
        public abstract void PropDirBrowseButton_Click(object sender, RoutedEventArgs e);

        public virtual string ProcessMethodReturnParameters(IEnumerable<object> elements)
        {
            var paramList = new List<string>();
            var addNull = false;
            elements.ForEach(item =>
            {
                
                if (item?.GetType() == typeof(TextBox))
                    paramList.Add(ProcessTextBox(item));
                else if (item?.GetType() == typeof(ComboBox))
                    paramList.Add(DataTableExtensions.EscapeSqlLikeValue(ProcessComboBox(item), true));
                if (item == null)
                    paramList.Add("null");

            });

            return paramList.Any() ? string.Join(", ", paramList) : string.Empty;
        }

        private string ProcessTextBox(object item)
        {
            string ret = string.Empty;

            var element = item as TextBox;
            ParamTab paramTab = element.Tag as ParamTab;
            if (paramTab != null)
            {
                ParameterInfo paramInfo = paramTab.ParamInfo;

                if (paramInfo.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<string>))
                    || paramInfo.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<int>))
                    || paramInfo.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<double>)))
                {
                    var splitter = element.Text.Split(new char[] { '\r', '\n' });
                    var parmToAdd = "[";

                    element.Text.Split(new char[] { '\r', '\n' })
                        .ForEach(parm =>
                        {
                            if (!string.IsNullOrEmpty(parm)) parmToAdd += "'" + DataTableExtensions.EscapeSqlLikeValue(parm) + "', ";
                        });

                    if (parmToAdd == "[") parmToAdd += "'', ";


                    parmToAdd = parmToAdd.Substring(0, parmToAdd.Length - 2);
                    parmToAdd += "]";

                    ret = parmToAdd;
                }
                else
                {
                     ret = "'" + HttpUtility.JavaScriptStringEncode(element.Text) + "'";
                }
            }

            return ret;
        }
        private string ProcessComboBox(object item)
        {
            string ret = string.Empty;

            var element = item as ComboBox;
            ParamTab paramTab = element.Tag as ParamTab;
            if (paramTab != null)
            {
                ParameterInfo paramInfo = paramTab.ParamInfo;

                ret = "'" + HttpUtility.JavaScriptStringEncode(element.Text) + "'";
            }

            return ret;
        }

        public virtual Engine Engine() => new Engine(cfg => cfg.AllowClr());

    }
}
