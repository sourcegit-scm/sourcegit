using System;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class LauncherTabSelectedEventArgs : RoutedEventArgs
    {
        public ViewModels.LauncherPage Page { get; }

        public LauncherTabSelectedEventArgs(ViewModels.LauncherPage page)
        {
            RoutedEvent = LauncherTabsSelector.PageSelectedEvent;
            Page = page;
        }
    }

    public partial class LauncherTabsSelector : UserControl
    {
        public static readonly StyledProperty<AvaloniaList<ViewModels.LauncherPage>> PagesProperty =
            AvaloniaProperty.Register<LauncherTabsSelector, AvaloniaList<ViewModels.LauncherPage>>(nameof(Pages));

        public AvaloniaList<ViewModels.LauncherPage> Pages
        {
            get => GetValue(PagesProperty);
            set => SetValue(PagesProperty, value);
        }

        public static readonly StyledProperty<string> SearchFilterProperty =
            AvaloniaProperty.Register<LauncherTabsSelector, string>(nameof(SearchFilter));

        public string SearchFilter
        {
            get => GetValue(SearchFilterProperty);
            set => SetValue(SearchFilterProperty, value);
        }

        public static readonly RoutedEvent<LauncherTabSelectedEventArgs> PageSelectedEvent =
            RoutedEvent.Register<LauncherTabsSelector, LauncherTabSelectedEventArgs>(nameof(PageSelected), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<LauncherTabSelectedEventArgs> PageSelected
        {
            add { AddHandler(PageSelectedEvent, value); }
            remove { RemoveHandler(PageSelectedEvent, value); }
        }

        public AvaloniaList<ViewModels.LauncherPage> VisiblePages
        {
            get;
            private set;
        }

        public LauncherTabsSelector()
        {
            VisiblePages = new AvaloniaList<ViewModels.LauncherPage>();
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == PagesProperty || change.Property == SearchFilterProperty)
                UpdateVisiblePages();
        }

        private void OnClearSearchFilter(object sender, RoutedEventArgs e)
        {
            SearchFilter = string.Empty;
        }

        private void OnPageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: ViewModels.LauncherPage page })
            {
                _isProcessingSelection = true;
                RaiseEvent(new LauncherTabSelectedEventArgs(page));
                _isProcessingSelection = false;
            }

            e.Handled = true;
        }

        private void UpdateVisiblePages()
        {
            if (_isProcessingSelection)
                return;

            VisiblePages.Clear();

            if (Pages == null)
                return;

            var filter = SearchFilter?.Trim() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                foreach (var p in Pages)
                    VisiblePages.Add(p);

                return;
            }

            foreach (var page in Pages)
            {
                if (!page.Node.IsRepository)
                    continue;

                if (page.Node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    page.Node.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    VisiblePages.Add(page);
            }
        }

        private bool _isProcessingSelection = false;
    }
}

