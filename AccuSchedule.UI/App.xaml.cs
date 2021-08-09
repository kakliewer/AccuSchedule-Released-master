using AccuSchedule.UI.Interfaces;
using AccuSchedule.UI.Models;
using AccuSchedule.UI.Views;
using MaterialDesignThemes.Wpf;
using Ninject;
using Ninject.Extensions.Conventions;
using Ninject.Extensions.Factory;
using Ninject.Syntax;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace AccuSchedule.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public List<ISetting> SettingsContainer { get; set; } = new List<ISetting>();

        public IKernel kernel = new StandardKernel();

        public App()
        {
            InjectToolPlugins();
        }


        public void InjectToolPlugins()
        {
            // Get the tool plugin directory
            var toolDIR = string.Format("{0}\\{1}", AppDomain.CurrentDomain.BaseDirectory, "Plugins\\Tools");
            if (!Directory.Exists(toolDIR)) Directory.CreateDirectory(toolDIR);

            // Bind any assemblies in the "Plugin/Tools" directory
            kernel.Bind(x =>
                x.FromAssembliesInPath(toolDIR)
                    .SelectAllClasses()
                    .InheritedFrom<IToolPlugin>()
                    .BindAllInterfaces());


            // Bind any tool classes in this assembly
            kernel.Bind(x =>
                x.FromThisAssembly()
                    .SelectAllClasses()
                    .InheritedFrom<IToolPlugin>()
                    .BindAllInterfaces());
        }

    }
}
