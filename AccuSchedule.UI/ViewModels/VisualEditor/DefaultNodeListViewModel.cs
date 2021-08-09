using AccuSchedule.UI.ViewModels.VisualEditor.Nodes;
using AccuSchedule.UI.Views.VisualEditor;
using DynamicData;
using NodeNetwork.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.ViewModels.VisualEditor
{
   
    //
    // Summary:
    //     A viewmodel for a UI List component that contains NodeViewModels and can be used
    //     to let the user add new nodes to a network.
    public class DefaultNodeListViewModel : ReactiveObject
    {
        static DefaultNodeListViewModel()
        {
            Splat.Locator.CurrentMutable.Register(() => new DefaultNodeListView(), typeof(IViewFor<DefaultNodeListViewModel>));
        }

        
        /// <summary>
        /// The formatting mode of the list.
        /// </summary>
        public enum DisplayMode
        {
            /// <summary>
            /// The nodes are displayed graphically in a grid.
            /// </summary>
            Tiles,
            /// <summary>
            /// The node names are displayed as text in a list.
            /// </summary>
            List
        }

        #region Title
        /// <summary>
        /// The string that is displayed at the top of the list
        /// </summary>
        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }
        private string _title;
        #endregion

        #region EmptyLabel
        /// <summary>
        /// The string that is displayed when VisibleNodes is empty.
        /// </summary>
        public string EmptyLabel
        {
            get => _emptyLabel;
            set => this.RaiseAndSetIfChanged(ref _emptyLabel, value);
        }
        private string _emptyLabel = "";
        #endregion


        #region DisplayMode
        /// <summary>
        /// The way the list of available nodes is formatted.
        /// </summary>
        public DisplayMode Display
        {
            get => _display;
            set => this.RaiseAndSetIfChanged(ref _display, value);
        }
        private DisplayMode _display;
        #endregion

        #region NodeFactories
        /// <summary>
        /// A dictionary of nodes and functions to create duplicate instances of them.
        /// To add a new node type to this list, DO NOT use this property but use AddNodeType instead.
        /// </summary>
        public Dictionary<DefaultNodeViewModel, Func<DefaultNodeViewModel>> NodeFactories { get; } = new Dictionary<DefaultNodeViewModel, Func<DefaultNodeViewModel>>();
        #endregion

        #region Nodes
        /// <summary>
        /// List of all the available nodes in the list.
        /// To add a new node type, do not use this list, use AddNodeType instead.
        /// </summary>
        public ISourceList<DefaultNodeViewModel> Nodes { get; } = new SourceList<DefaultNodeViewModel>();
        #endregion

        #region VisibleNodes
        /// <summary>
        /// List of nodes that are actually visible in the list.
        /// This list is based on Nodes and SearchQuery.
        /// </summary>
        public IObservableList<DefaultNodeViewModel> VisibleNodes { get; }
        #endregion

        #region SearchQuery
        /// <summary>
        /// The current search string that is used to filter Nodes into VisibleNodes.
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
        }
        private string _searchQuery = "";
        #endregion

        public DefaultNodeListViewModel()
        {
            Title = "Blocks";
            EmptyLabel = "No matching blocks found.";
            Display = DisplayMode.List;

            var onQueryChanged = this.WhenAnyValue(vm => vm.SearchQuery)
                .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
                .Publish();
            onQueryChanged.Connect();
            VisibleNodes = Nodes.Connect()
                .AutoRefreshOnObservable(_ => onQueryChanged)
                .AutoRefresh(node => node.Name)
                .Filter(n => (n.Name ?? "").ToUpper().Contains(SearchQuery?.ToUpper() ?? ""))
                .AsObservableList();
        }

        /// <summary>
        /// Adds a new node type to the list.
        /// Every time a node is added to a network from this list, the factory function will be called to create a new instance of the viewmodel type.
        /// </summary>
        /// <typeparam name="T">The subtype of NodeViewModel to add to the list.</typeparam>
        /// <param name="factory">The factory function to create a new instance of T</param>
        public void AddNodeType<T>(Func<T> factory) where T : DefaultNodeViewModel
        {
            var instance = factory();
            NodeFactories.Add(instance, factory);
            Nodes.Add(instance);
            
        }
    }
}


