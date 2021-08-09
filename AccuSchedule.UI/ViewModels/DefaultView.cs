using AccuSchedule.UI.Extensions;
using AccuSchedule.UI.Interfaces;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using Dragablz;
using System.Windows.Media;
using AccuSchedule.UI.Test;
using System.Reflection;
using AccuSchedule.UI.Models;
using MaterialDesignThemes.Wpf;
using System.Web.Script.Serialization;
using AccuSchedule.UI.Methods;
using System.Data;

namespace AccuSchedule.UI.Views
{
    [Serializable]
    public class DefaultView : ViewBuilderBase<DockPanel, StackPanel, DockPanel, TabControl, TabItem>
    {
        public Grid RootGrid { get; set; } // Root Element of Window
        public StackPanel ToolsContainer { get; set; }
        public StackPanel SectionsContainer { get; set; }


        public override Type[] AllowedTypes => 
            new Type[] {  }; // Only allow these types in the view

        public DefaultView(Grid rootGrid)
        {
            RootGrid = rootGrid;

            CreateContainer();

            CreateRHPane();

        }

        #region Containers
        public virtual Grid RHView()
        {

            var GridView = new Grid();
            List<string> tabNames = new List<string>() { "Information", "Chart" };

            var tc = new TabControl();
            // Create new tabs
            tabNames.ForEach(tab =>
            {
                var tabItem = new TabItem() { Header = tab, Margin = new Thickness(0, 0, 2, 0) };
                if (tab == "Information")
                {
                    tabItem.Content = "Information";
                }
                else
                {
                    tabItem.Content = "Chart";
                }

                ControlsHelper.SetHeaderFontSize(tabItem, 18);
                tc.Items.Add(tabItem);
            });
            GridView.Children.Add(tc);

            return GridView;
        }

        public override DockPanel CreateContainer()
        {
            Container = new DockPanel();

            // Add Tools panel and attach scroll bars
            var scrollViewer = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 2, 0)
            };
            LHPane = new StackPanel()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 2, 0)
            };
            DockPanel.SetDock(LHPane, Dock.Left);


            // Create Tools Container
            ToolsContainer = new StackPanel();
            var expander = new Expander()
            {
                Header = new TextBlock()
                {
                    Text = "Tools",
                    LayoutTransform = new RotateTransform()
                    {
                        Angle = 90
                    },
                    RenderTransformOrigin = new Point(.5, .5)
                },
                ExpandDirection = ExpandDirection.Left,
                FontSize = 16,
                FontWeight = FontWeights.DemiBold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Effect = new DropShadowEffect()
                {
                    Opacity = .2,
                    ShadowDepth = 6
                },
                Content = ToolsContainer
            };

            SectionsContainer = new StackPanel();
            expander.Content = SectionsContainer;

            LHPane.Children.Add(expander);

            // Attach Scroll Viewer to Pane and add to main container
            scrollViewer.Content = LHPane; 
            Container.Children.Add(scrollViewer);

            return Container;
        }
        public override DockPanel CreateRHPane()
        {
            

            RHPane = new DockPanel();
            Container.Children.Add(RHPane);
            DockPanel.SetDock(RHPane, Dock.Right);

            PlansContainer = CreatePlansContainer();

            // Add control to RH Pane
            RHPane.Children.Add(PlansContainer);

            return RHPane;
        }
        public override TabControl CreatePlansContainer() => new TabControl()
        {
            FontSize = 10,
            Margin = new Thickness(0, 0, 2, 2)
        };
        public override void AddPlan(TabItem PlanHeaderControl) => PlansContainer.Items.Add(PlanHeaderControl);
        public override void RenamePlan(TabItem PlanHeaderControl, string newName) => PlanHeaderControl.Header = newName;
        public override void RemovePlan(TabItem PlanHeaderControl) => PlansContainer.Items.Remove(PlanHeaderControl);
        public override void RemovePlan(int index) => PlansContainer.Items.RemoveAt(index);
        public override void ClearPlans() => PlansContainer.Items.Clear();
        #endregion

        #region LH Menu
        public override void AddMenuItem(IToolPlugin ToolsClass, Type ReturnTypeRestriction = null)
        {
            if (ToolsContainer == null) return;

            // Get Section Name from Class
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

            // Find existing container or add new
            var section = CheckIfLHSectionExists<Expander>(SectionsContainer, new HeaderedContentControl() { Header = sectionName });
            var itemPanel = new StackPanel(); // holds the items for each section

            // Check if we need to add new section
            if (section == null)
            {
                section = new Expander()
                {
                    Header = sectionName,
                    FontSize = 12,
                    Margin = new Thickness(20, 2, 0, 0),
                    Padding = new Thickness(10, 0, 2, 0),
                    FontWeight = FontWeights.DemiBold,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Add Item StackPanel
                section.Content = itemPanel;
                SectionsContainer.Children.Add(section);
            } 
            else
            {
                itemPanel = section.Content as StackPanel;
            }

            // Add items to Section
            //GetToolsClassItems(ToolsClass
            //    , item =>
            //    {
            //        itemPanel.Children.Add(item);
            //        return item;
            //    }
            //    , ReturnTypeRestriction);

        }
        public override void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var ctrl = sender as CheckBox;
            if (ctrl != null)
            {
                if (ctrl.IsChecked == true)
                {

                    // Get the method info from the controls Tag propert
                    var method = (ToolID)ctrl.Tag;

                    if (method.ToolMethodInfo.GetParameters().Count() > 0)
                    {
                        var popUp = PopUp(method, false, DialogButtons.OK, DialogButtons.Cancel);

                        // Create new view for PopUp
                        StackPanel stackPanel = new StackPanel()
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch
                        };
                        var footer = PopUpFooterControls(method, DialogButtons.OK, DialogButtons.Cancel);
                        var parameters = ProcessMethodInputParameters(method.ToolMethodInfo);
                        foreach (var param in parameters) stackPanel.Children.Add(param);

                        stackPanel.Children.Add(footer);

                        // Check if PopUp window exists
                        DialogHost dialogHost = null;
                        foreach (var child in RootGrid.Children)
                        {
                            DialogHost dialog = child as DialogHost;
                            if (dialog != null)
                            {
                                if (dialog.Name == "PopUpWindow")
                                {
                                    dialogHost = dialog;
                                    break;
                                }
                            }
                        }

                        // Add the dialog if it doesn't exist
                        if (dialogHost == null)
                        {
                            RootGrid.Children.Add(popUp);
                            dialogHost = popUp;
                        }

                        dialogHost.ShowDialog(stackPanel);
                    } else
                    {
                       // throw new NotImplementedException();
                    }
                } else
                {
                    //throw new NotImplementedException();
                }
            } 

        }
        static void Executioner<PayloadType>(ToolID method, string toExecute)
        {
            if (string.IsNullOrEmpty(toExecute)) throw new NullReferenceException();

            // Create instance of class
            object obj = Activator.CreateInstance(method.ToolType);

            // Execute the method
            var tools = new Tools();
            var command = tools.Engine()
                .SetValue("jintObj", obj)
                .Execute(@"jintObj." + method.ToolMethodInfo.Name + @"(" + toExecute + ");")
                .GetCompletionValue()
                .ToObject();
        }
        #endregion

        #region Dialog
        public override void DialogFooterControl_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var btnParent = btn.Parent as StackPanel;
            var host = btnParent.GetAncestorOfType<StackPanel>();
            var targets = host.FindChildren<TextBox>();


            if (btn != null)
            {
                switch (btn.Content)
                {
                    case "OK":
                        //Executioner((ToolID)btn.Tag, ProcessMethodReturnParameters(targets));
                        break;
                    case "Accept":
                        //Executioner((ToolID)btn.Tag, ProcessMethodReturnParameters(targets));
                        break;
                    case "Cancel":
                        break;
                    default:
                        break;
                }
            }
        }

        


        #endregion
    }
}
