using AccuSchedule.UI.ViewModels.VisualEditor;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AccuSchedule.UI.Views.VisualEditor
{
    /// <summary>
    /// Interaction logic for DefaultNodeListView.xaml
    /// </summary>
    public partial class DefaultNodeListView : IViewFor<DefaultNodeListViewModel>
    {
        #region ViewModel
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
            typeof(DefaultNodeListViewModel), typeof(DefaultNodeListView), new PropertyMetadata(null));

        public DefaultNodeListViewModel ViewModel
        {
            get => (DefaultNodeListViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (DefaultNodeListViewModel)value;
        }
        #endregion

        #region Show/Hide properties
        public static readonly DependencyProperty ShowSearchProperty =
            DependencyProperty.Register(nameof(ShowSearch), typeof(bool), typeof(DefaultNodeListView), new PropertyMetadata(true));
        public static readonly DependencyProperty ShowDisplayModeSelectorProperty =
            DependencyProperty.Register(nameof(ShowDisplayModeSelector), typeof(bool), typeof(DefaultNodeListView), new PropertyMetadata(true));
        public static readonly DependencyProperty ShowTitleProperty =
            DependencyProperty.Register(nameof(ShowTitle), typeof(bool), typeof(DefaultNodeListView), new PropertyMetadata(true));

        public bool ShowSearch
        {
            get { return (bool)GetValue(ShowSearchProperty); }
            set { SetValue(ShowSearchProperty, value); }
        }

        public bool ShowDisplayModeSelector
        {
            get { return (bool)GetValue(ShowDisplayModeSelectorProperty); }
            set { SetValue(ShowDisplayModeSelectorProperty, value); }
        }

        public bool ShowTitle
        {
            get { return (bool)GetValue(ShowTitleProperty); }
            set { SetValue(ShowTitleProperty, value); }
        }
        #endregion

        public CollectionViewSource CVS { get; } = new CollectionViewSource();

        public DefaultNodeListView()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) { return; }

            this.WhenActivated(d =>
            {

                this.OneWayBind(ViewModel, vm => vm.Display, v => v.elementsList.ItemTemplate,
                    displayMode => displayMode == DefaultNodeListViewModel.DisplayMode.Tiles
                        ? Resources["tilesTemplate"]
                        : Resources["listTemplate"])
                    .DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.Display, v => v.elementsList.ItemsPanel,
                    displayMode => displayMode == DefaultNodeListViewModel.DisplayMode.Tiles
                        ? Resources["tilesItemsPanelTemplate"]
                        : Resources["listItemsPanelTemplate"])
                    .DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.Display, v => v.elementsList.Template,
                    displayMode => displayMode == DefaultNodeListViewModel.DisplayMode.Tiles
                        ? Resources["tilesItemsControlTemplate"]
                        : Resources["listItemsControlTemplate"])
                    .DisposeWith(d);

                this.Bind(ViewModel, vm => vm.SearchQuery, v => v.searchBox.Text).DisposeWith(d);

                this.WhenAnyValue(v => v.ViewModel.VisibleNodes).Switch().Bind(out var bindableList).Subscribe().DisposeWith(d);
                CVS.Source = bindableList;
                elementsList.ItemsSource = CVS.View;

                this.WhenAnyObservable(v => v.ViewModel.VisibleNodes.CountChanged)
                    .Select(count => count == 0)
                    .BindTo(this, v => v.emptyMessage.Visibility).DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.Title, v => v.titleLabel.Text).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.EmptyLabel, v => v.emptyMessage.Text).DisposeWith(d);

                this.WhenAnyValue(v => v.searchBox.IsFocused, v => v.searchBox.Text)
                    .Select(t => !t.Item1 && string.IsNullOrWhiteSpace(t.Item2))
                    .BindTo(this, v => v.emptySearchBoxMessage.Visibility)
                    .DisposeWith(d);

                this.WhenAnyValue(v => v.ShowSearch)
                    .BindTo(this, v => v.searchBoxGrid.Visibility).DisposeWith(d);
                this.WhenAnyValue(v => v.ShowTitle)
                    .BindTo(this, v => v.titleLabel.Visibility).DisposeWith(d);
            });
        }

        private void OnNodeMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DefaultNodeViewModel nodeVM = ((FrameworkElement)sender).DataContext as DefaultNodeViewModel;
                if (nodeVM == null)
                {
                    return;
                }

                DefaultNodeViewModel newNodeVM = ViewModel.NodeFactories[nodeVM]();

                DragDrop.DoDragDrop(this, new DataObject("nodeVM", newNodeVM), DragDropEffects.Copy);
            }
        }
    }

}

