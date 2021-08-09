using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Effects;
using System.Xml;
using AccuSchedule.UI;
using AccuSchedule.UI.Models;
using MaterialDesignThemes.Wpf;

namespace AccuSchedule.UI.Interfaces
{
    public interface IViewLoader<ContainerType, LHPaneType, RHPaneType, PlansContainerType, PlanItemType>
    {
        ContainerType Container { get; set; }
        LHPaneType LHPane { get; set; }
        RHPaneType RHPane { get; set; }
        PlansContainerType PlansContainer { get; set; }
    }


    public abstract class ViewBuilderBase<ContainerType, LHPaneType, RHPaneType, PlansContainerType, PlanItemType> : IViewLoader<ContainerType, LHPaneType, RHPaneType, PlansContainerType, PlanItemType>
    {
        public ContainerType Container { get; set; }
        public LHPaneType LHPane { get; set; }
        public RHPaneType RHPane { get; set; }
        public PlansContainerType PlansContainer { get; set; }

        public abstract Type[] AllowedTypes { get; }

        #region Tools
        public virtual void GetToolsClassItems<PayloadType>(object SectionTools, Func<Control, Control> AddToPaneFunction , Type ReturnTypeRestriction)
        {
            // Add each public method in the class
            SectionTools.GetType()
                .GetMethods(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
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
                        CheckBox menuBtn =
                                new CheckBox()
                                {
                                    Content = method.Name,
                                    Tag = new ToolID() { ToolType = SectionTools.GetType(), ToolMethodInfo = method },
                                    FontSize = 12,
                                    Margin = new Thickness(20, 2, 0, 0),
                                    Padding = new Thickness(10, 0, 2, 0),
                                    FontWeight = FontWeights.DemiBold,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Center
                                };

                        menuBtn.Click += MenuItem_Click; // Assign the click handler
                        AddToPaneFunction(menuBtn);

                    }
                });

        }
        public static Grid CreateGrid(int cols, int rows, bool resizable = true, double ColWidth = 150, double RowHeight = 150, bool lastResize = true, bool showGridLines = false, bool staticLHPane = false)
        {
            var ret = new Grid();
            ret.ShowGridLines = showGridLines;
            ret.HorizontalAlignment = HorizontalAlignment.Stretch;

            // Add Column Definitions
            for (int c = 0; c < cols; c++)
            {
                ret.ColumnDefinitions.Add(new ColumnDefinition());
                if (c == cols - 1 && lastResize)
                {
                    _ = !double.IsNaN(ColWidth)
                        ? ret.ColumnDefinitions.Last().Width = new GridLength(ColWidth, GridUnitType.Pixel)
                        : ret.ColumnDefinitions.Last().Width = new GridLength(1.0, GridUnitType.Auto);
                }
                else if (!double.IsNaN(ColWidth) && c == 0 && !lastResize)
                {
                    _ = !double.IsNaN(ColWidth)
                        ? ret.ColumnDefinitions.First().Width = new GridLength(ColWidth, GridUnitType.Pixel)
                        : ret.ColumnDefinitions.First().Width = new GridLength(1.0, GridUnitType.Auto);
                }
            }

            // Add Row Definitions
            if (rows > 1)
            {
                for (int r = 0; r < rows; r++)
                {
                    ret.RowDefinitions.Add(new RowDefinition());
                    if (!double.IsNaN(RowHeight) && r == rows - 1 && lastResize)
                    {
                        ret.RowDefinitions.Last().Height = new GridLength(RowHeight, GridUnitType.Pixel);
                    }
                    else if (!double.IsNaN(RowHeight) && r == 0 && !lastResize)
                    {
                        ret.RowDefinitions.First().Height = new GridLength(RowHeight, GridUnitType.Pixel);
                    }
                }
            }



            // Add resizer between LH and RH panes
            if (resizable)
            {
                // Set Column Resizer
                if (cols <= 2)
                { // Single Adjuster
                    if (!staticLHPane)
                    {
                        var vGridSplitter = new GridSplitter()
                        {
                            Width = 2,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Stretch
                        };
                        ret.Children.Add(vGridSplitter);
                        Grid.SetColumn(vGridSplitter, 0);
                        if (rows > 0) Grid.SetRowSpan(vGridSplitter, rows);
                    }
                }
                else
                { // Multi-Adjuster
                    for (int i = 0; i < cols - 1; i++)
                    {
                        if (!staticLHPane && i == 0)
                        {
                            var vGridSplitter = new GridSplitter()
                            {
                                Width = 2,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Stretch
                            };
                            ret.Children.Add(vGridSplitter);
                            Grid.SetColumn(vGridSplitter, i);
                            if (rows > 0) Grid.SetRowSpan(vGridSplitter, rows);
                        }
                    }
                }


                // Set Row Resizer
                if (rows >= 2)
                { // Multi-Adjuster
                    for (int i = 0; i < rows - 1; i++)
                    {
                        var hGridSplitter = new GridSplitter()
                        {
                            Height = 2,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                            VerticalAlignment = System.Windows.VerticalAlignment.Bottom
                        };
                        ret.Children.Add(hGridSplitter);
                        Grid.SetRow(hGridSplitter, i);
                        Grid.SetColumnSpan(hGridSplitter, rows);
                    }
                }
            }

            return ret;
        }
        public virtual List<UIElement> ProcessMethodInputParameters(MethodInfo method)
        {
            List<UIElement> ret = new List<UIElement>();

            foreach (var param in method.GetParameters())
            {
                var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };

                if (param.ParameterType == typeof(string) || param.ParameterType == typeof(int) || param.ParameterType == typeof(double))
                {
                    var name = new Label() { Content = string.Format("{0} ({1})", param.Name, param.ParameterType.Name) };
                    var value = new TextBox() { Tag = param, TextWrapping = TextWrapping.Wrap, MinWidth = 100, AcceptsReturn = false };
                    stackPanel.Children.Add(name);
                    stackPanel.Children.Add(value);
                    ret.Add(stackPanel);
                }
                else if (param.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<string>))
                    || param.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<int>))
                    || param.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<double>)))
                {
                    var name = new Label() { Content = string.Format("{0} ({1})", param.Name, param.ParameterType.Name) };
                    var value = new TextBox() { Tag = param, TextWrapping = TextWrapping.Wrap, MinWidth = 100, AcceptsReturn = true };
                    stackPanel.Children.Add(name);
                    stackPanel.Children.Add(value);
                    ret.Add(stackPanel);
                }
            }

            return ret;
        }
        public virtual string ProcessMethodReturnParameters(IEnumerable<TextBox> elements)
        {
            var paramList = new HashSet<string>();
            elements.ForEach(item =>
            {
                ParameterInfo paramInfo = item.Tag as ParameterInfo;
                if (paramInfo.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<string>))
                    || paramInfo.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<int>))
                    || paramInfo.ParameterType.GetInterfaces().Contains(typeof(IEnumerable<double>)))
                {
                    var splitter = item.Text.Split(new char[] { '\r', '\n' });
                    var parmToAdd = "[";

                    item.Text.Split(new char[] { '\r', '\n' })
                        .ForEach(parm => {
                            if (!string.IsNullOrEmpty(parm)) parmToAdd += "'" + parm + "', ";
                        });

                    parmToAdd = parmToAdd.Substring(0, parmToAdd.Length - 2);
                    parmToAdd += "]";

                    paramList.Add(parmToAdd);
                }
                else
                {
                    paramList.Add("'" + item.Text + "'");
                }
            });
            var ret = string.Join(", ", paramList);
            return ret;
        }
        public virtual objectType CheckIfLHSectionExists<objectType>(Panel ToolsContainer, HeaderedContentControl compareText)
        {
            if (ToolsContainer == null) return default(objectType);

            objectType foundItem = default(objectType);
            foreach (var item in ToolsContainer.Children)
            {
                if (item.GetType() == typeof(objectType))
                {
                    var itemText = item as HeaderedContentControl;
                    if (itemText.Header == compareText.Header)
                    {
                        foundItem = (objectType)item;
                        break; // Only look for 1 instance
                    }
                    
                }
            }

            return foundItem;
        }
        #endregion

        #region Dialog
        public enum DialogButtons
        {
            OK,
            Cancel,
            Accept
        }
        public virtual DialogHost PopUp(ToolID tooldID, bool closeOnClickAway = false, params DialogButtons[] footerControls)
        {
            var dialog = new DialogHost
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CloseOnClickAway = closeOnClickAway,
                Tag = tooldID,
                Name = "PopUpWindow"
            };

            dialog.DialogClosing += PopUp_DialogClosing;
            dialog.DialogOpened += PopUp_Opened;

            return dialog;
        }
        private static void PopUp_Opened(object sender, DialogOpenedEventArgs eventArgs)
        {
            //throw new NotImplementedException();
        }
        public static void PopUp_DialogClosing(object sender, DialogClosingEventArgs eventArgs)
        {
            //throw new NotImplementedException();
        }
        public virtual StackPanel PopUpFooterControls(ToolID method, params DialogButtons[] FooterControls)
        {
            var stackPanel = new StackPanel() { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal };
            foreach (var item in FooterControls)
            {
                switch (item)
                {
                    case DialogButtons.OK:
                        var okButton = new Button()
                        {
                            Content = DialogButtons.OK.ToString(),
                            Command = DialogHost.CloseDialogCommand,
                            CommandParameter = true,
                            Margin = new Thickness(15, 5, 5, 5),
                            Tag = method
                        };
                        okButton.Click += DialogFooterControl_Click;
                        stackPanel.Children.Add(okButton);
                        break;
                    case DialogButtons.Cancel:
                        var cancelButton = new Button()
                        {
                            Content = DialogButtons.Cancel.ToString(),
                            Command = DialogHost.CloseDialogCommand,
                            CommandParameter = false,
                            Margin = new Thickness(15, 5, 5, 5),
                            Tag = method
                        };
                        cancelButton.Click += DialogFooterControl_Click;
                        stackPanel.Children.Add(cancelButton);
                        break;
                    case DialogButtons.Accept:
                        var acceptButton = new Button()
                        {
                            Content = DialogButtons.Accept.ToString(),
                            Command = DialogHost.CloseDialogCommand,
                            CommandParameter = true,
                            Margin = new Thickness(15, 5, 5, 5),
                            Tag = method
                        };
                        acceptButton.Click += DialogFooterControl_Click;
                        stackPanel.Children.Add(acceptButton);
                        break;
                    default:
                        break;
                }
            }
            return stackPanel;
        }
        public abstract void DialogFooterControl_Click(object sender, RoutedEventArgs e);
        #endregion

        #region Containers
        public abstract ContainerType CreateContainer();
        public abstract RHPaneType CreateRHPane();

        public abstract PlansContainerType CreatePlansContainer();

        
        public abstract void MenuItem_Click(object sender, RoutedEventArgs e);
        public abstract void AddMenuItem(IToolPlugin ToolsClass, Type ReturnTypeRestriction);
        public abstract void AddPlan(PlanItemType PlanHeaderControl);
        public abstract void RenamePlan(PlanItemType PlanHeaderControl, string newName);
        public abstract void RemovePlan(PlanItemType PlanHeaderControl);
        public abstract void RemovePlan(int PlanHeaderControl);
        public abstract void ClearPlans();
        #endregion

    }

}
