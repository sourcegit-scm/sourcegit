using System;
using System.Collections.Generic;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class DevSpaces : UserControl, IDisposable
    {
        private sealed record TerminalPaneHandle(Border Root, DevSpaceTerminal TerminalView);

        public DevSpaces()
        {
            InitializeComponent();
            AIRouterView.DataContext = new ViewModels.DevSpaceAIRouter();
            DataContextChanged += OnDataContextChanged;
        }

        public void SetPageActive(bool active)
        {
            _pageActive = active;
            UpdateSurfaceVisibility();
        }

        public void Dispose()
        {
            if (_owner != null)
                _owner.PropertyChanged -= OnOwnerPropertyChanged;
            DisposePanes();
            _owner = null;
            DataContext = null;
        }

        private void OnDataContextChanged(object sender, EventArgs e)
        {
            var next = DataContext as ViewModels.DevSpaces;
            if (ReferenceEquals(_owner, next))
                return;
            if (_owner != null)
                _owner.PropertyChanged -= OnOwnerPropertyChanged;
            DisposePanes();
            _owner = next;
            if (_owner != null)
                _owner.PropertyChanged += OnOwnerPropertyChanged;
            UpdatePageVisibility();
            RebuildGrid();
        }

        private void OnOwnerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.DevSpaces.ActivePage) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.IsDashboardActive) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.IsFilesActive) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.IsAIRouterActive) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.IsTerminalsActive) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.IsRoslynActive))
                Dispatcher.UIThread.Post(UpdatePageVisibility);

            if (e.PropertyName == nameof(ViewModels.DevSpaces.VisibleSlots) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.GridRows) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.GridColumns) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.ActiveTerminal) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.Layout) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.TerminalDisplayMode))
                Dispatcher.UIThread.Post(RebuildGrid);
        }

        private void OnCreateTerminal(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
                ShowTerminalPicker(control, -1);
            e.Handled = true;
        }

        private void OnGridLayout(object sender, RoutedEventArgs e)
        {
            if (_owner != null)
                _owner.TerminalDisplayMode = Models.DevSpaceTerminalDisplayMode.Grid;
            e.Handled = true;
        }

        private void OnListLayout(object sender, RoutedEventArgs e)
        {
            if (_owner != null)
                _owner.TerminalDisplayMode = Models.DevSpaceTerminalDisplayMode.List;
            e.Handled = true;
        }

        private void OnDashboardTabPressed(object sender, PointerPressedEventArgs e)
        {
            _owner?.ActivateDashboard();
            e.Handled = true;
        }

        private void OnFilesTabPressed(object sender, PointerPressedEventArgs e)
        {
            _owner?.ActivateFiles();
            e.Handled = true;
        }

        private void OnAIRouterTabPressed(object sender, PointerPressedEventArgs e)
        {
            _owner?.ActivateAIRouter();
            e.Handled = true;
        }

        private void OnTerminalsTabPressed(object sender, PointerPressedEventArgs e)
        {
            _owner?.ActivateTerminals();
            e.Handled = true;
        }

        private void OnTerminalTabPressed(object sender, PointerPressedEventArgs e)
        {
            if (_owner != null && sender is Border { DataContext: ViewModels.DevSpaceTerminal session })
                _owner.ActivateTerminal(session);
            e.Handled = true;
        }

        private void OnCloseTerminal(object sender, RoutedEventArgs e)
        {
            if (_owner != null && sender is Button { DataContext: ViewModels.DevSpaceTerminal session })
                CloseTerminal(session);
            e.Handled = true;
        }

        private void CloseTerminal(ViewModels.DevSpaceTerminal session)
        {
            _owner?.CloseTerminal(session);
            if (_panes.Remove(session.Id, out var pane))
            {
                pane.TerminalView.SetPageActive(false);
                TerminalGrid.Children.Remove(pane.Root);
                pane.TerminalView.Dispose();
            }
            RebuildGrid();
        }

        private void UpdatePageVisibility()
        {
            var showDashboard = _owner?.IsDashboardActive == true;
            var showFiles = _owner?.IsFilesActive == true;
            var showAIRouter = _owner?.IsAIRouterActive == true;
            var showTerminals = _owner?.IsTerminalsActive == true;
            DashboardView.IsVisible = showDashboard;
            DashboardView.IsHitTestVisible = showDashboard;
            FilesView.IsVisible = showFiles;
            FilesView.IsHitTestVisible = showFiles;
            AIRouterView.IsVisible = showAIRouter;
            AIRouterView.IsHitTestVisible = showAIRouter;
            TerminalGrid.IsVisible = true;
            TerminalGrid.Opacity = showTerminals ? 1 : 0;
            TerminalGrid.IsHitTestVisible = showTerminals;
            UpdateSurfaceVisibility();
        }

        private void RebuildGrid()
        {
            ClearEmptySlots();
            if (_owner == null)
            {
                TerminalGrid.RowDefinitions = new RowDefinitions("*");
                TerminalGrid.ColumnDefinitions = new ColumnDefinitions("*");
                UpdateSurfaceVisibility();
                return;
            }

            TerminalGrid.RowDefinitions = CreateRowDefinitions(_owner.GridRows);
            TerminalGrid.ColumnDefinitions = CreateColumnDefinitions(_owner.GridColumns);
            foreach (var session in _owner.Sessions)
                GetOrCreatePane(session);

            foreach (var pane in _panes.Values)
            {
                pane.Root.Opacity = 0;
                pane.Root.IsHitTestVisible = false;
                pane.Root.ZIndex = 0;
                Grid.SetRow(pane.Root, 0);
                Grid.SetColumn(pane.Root, 0);
            }

            foreach (var slot in _owner.VisibleSlots)
            {
                var row = slot.Index / _owner.GridColumns;
                var column = slot.Index % _owner.GridColumns;
                if (slot.Terminal != null)
                {
                    var pane = GetOrCreatePane(slot.Terminal);
                    Grid.SetRow(pane, row);
                    Grid.SetColumn(pane, column);
                    pane.Opacity = 1;
                    pane.IsHitTestVisible = true;
                    pane.ZIndex = 1;
                }
                else if (_owner.IsGridLayout && _owner.Layout != Models.DevSpaceLayout.Auto)
                {
                    var empty = CreateEmptySlot(slot.Index);
                    Grid.SetRow(empty, row);
                    Grid.SetColumn(empty, column);
                    TerminalGrid.Children.Add(empty);
                    _emptySlots.Add(empty);
                }
            }
            UpdateSurfaceVisibility();
        }

        private void UpdateSurfaceVisibility()
        {
            foreach (var pane in _panes.Values)
                pane.TerminalView.SetPageActive(false);
            if (!_pageActive || _owner?.IsTerminalsActive != true)
                return;
            foreach (var slot in _owner.VisibleSlots)
            {
                if (slot.Terminal != null && _panes.TryGetValue(slot.Terminal.Id, out var pane))
                    pane.TerminalView.SetPageActive(true);
            }
        }

        private Border GetOrCreatePane(ViewModels.DevSpaceTerminal session)
        {
            if (_panes.TryGetValue(session.Id, out var cached))
                return cached.Root;
            var terminalView = new DevSpaceTerminal { DataContext = session };
            var title = new TextBlock
            {
                Text = session.Title,
                Margin = new Thickness(8, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var close = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Content = "×",
                DataContext = session,
            };
            close.Classes.Add("icon_button");
            close.Click += (_, e) =>
            {
                CloseTerminal(session);
                e.Handled = true;
            };
            ToolTip.SetTip(close, App.Text("DevSpaces.CloseTerminal"));
            var header = new Grid
            {
                Height = 28,
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };
            header.Children.Add(title);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.PointerPressed += (_, e) =>
            {
                _owner?.ActivateTerminal(session);
                e.Handled = true;
            };
            var content = new Grid { RowDefinitions = new RowDefinitions("28,*") };
            content.Children.Add(header);
            Grid.SetRow(terminalView, 1);
            content.Children.Add(terminalView);
            var root = new Border
            {
                Margin = new Thickness(2),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = content,
            };
            _panes.Add(session.Id, new TerminalPaneHandle(root, terminalView));
            terminalView.Start(_owner.Launcher);
            TerminalGrid.Children.Add(root);
            return root;
        }

        private Button CreateEmptySlot(int slotIndex)
        {
            var button = new Button
            {
                Margin = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = App.Text("DevSpaces.NewTerminal"),
                Tag = slotIndex,
            };
            button.Classes.Add("flat");
            button.Click += (_, e) =>
            {
                ShowTerminalPicker(button, slotIndex);
                e.Handled = true;
            };
            return button;
        }

        private void ShowTerminalPicker(Control target, int preferredSlot)
        {
            if (_owner == null)
                return;
            var settings = SourceGit.DevSpaces.DevSpaceProfileSettings.Instance;
            var flyout = new MenuFlyout();
            var defaultName = SourceGit.DevSpaces.DevSpaceProfileSettings.GetTerminalDisplayName(settings.DefaultTerminal);
            flyout.Items.Add(CreateTerminalMenuItem($"New {defaultName}", settings.DefaultTerminal, defaultName, preferredSlot));
            foreach (var agent in SourceGit.DevSpaces.DevSpaceAgent.BuiltIn)
                flyout.Items.Add(CreateAgentMenuItem(agent, preferredSlot));
            if (settings.Profiles.Count > 0)
            {
                flyout.Items.Add(new Separator());
                foreach (var profile in settings.Profiles)
                    flyout.Items.Add(CreateProfileMenuItem(profile, preferredSlot));
            }
            flyout.Items.Add(new Separator());
            var terminals = new MenuItem { Header = "Terminal" };
            foreach (var choice in SourceGit.DevSpaces.DevSpaceProfileSettings.SupportedTerminals)
            {
                terminals.Items.Add(CreateTerminalMenuItem(
                    choice.Name,
                    choice.Value,
                    SourceGit.DevSpaces.DevSpaceProfileSettings.GetTerminalDisplayName(choice.Value),
                    preferredSlot));
            }
            flyout.Items.Add(terminals);
            var manage = new MenuItem { Header = "Manage Profiles…" };
            manage.Click += async (_, e) =>
            {
                e.Handled = true;
                if (TopLevel.GetTopLevel(this) is Window owner)
                    await new DevSpaceProfileManager().ShowDialog(owner);
            };
            flyout.Items.Add(manage);
            flyout.ShowAt(target);
        }

        private MenuItem CreateAgentMenuItem(SourceGit.DevSpaces.DevSpaceAgent agent, int preferredSlot)
        {
            var item = new MenuItem { Header = agent.Name };
            item.Click += (_, e) =>
            {
                _owner?.CreateAgentTerminalAt(preferredSlot, agent);
                e.Handled = true;
            };
            return item;
        }

        private MenuItem CreateProfileMenuItem(SourceGit.DevSpaces.DevSpaceTerminalProfile profile, int preferredSlot)
        {
            var path = string.IsNullOrWhiteSpace(profile.Path) ? "." : profile.Path;
            var item = new MenuItem { Header = $"{profile.DisplayName}  ·  {path}" };
            item.Click += async (_, e) =>
            {
                e.Handled = true;
                try
                {
                    _owner?.CreateProfileTerminalAt(preferredSlot, profile);
                }
                catch (Exception ex)
                {
                    if (TopLevel.GetTopLevel(this) is Window owner)
                        await new Alert().ShowAsync(owner, ex.Message, true);
                }
            };
            return item;
        }

        private MenuItem CreateTerminalMenuItem(string header, string terminal, string displayName, int preferredSlot)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, e) =>
            {
                _owner?.CreateTerminalAt(preferredSlot, terminal, displayName);
                e.Handled = true;
            };
            return item;
        }

        private static RowDefinitions CreateRowDefinitions(int count)
        {
            var definitions = new RowDefinitions();
            for (var i = 0; i < Math.Max(1, count); i++)
                definitions.Add(new RowDefinition(GridLength.Star));
            return definitions;
        }

        private static ColumnDefinitions CreateColumnDefinitions(int count)
        {
            var definitions = new ColumnDefinitions();
            for (var i = 0; i < Math.Max(1, count); i++)
                definitions.Add(new ColumnDefinition(GridLength.Star));
            return definitions;
        }

        private void ClearEmptySlots()
        {
            foreach (var empty in _emptySlots)
                TerminalGrid.Children.Remove(empty);
            _emptySlots.Clear();
        }

        private void DisposePanes()
        {
            foreach (var pane in _panes.Values)
            {
                pane.TerminalView.SetPageActive(false);
                pane.TerminalView.Dispose();
            }
            _panes.Clear();
            _emptySlots.Clear();
            TerminalGrid.Children.Clear();
        }

        private readonly Dictionary<Guid, TerminalPaneHandle> _panes = [];
        private readonly List<Button> _emptySlots = [];
        private ViewModels.DevSpaces _owner;
        private bool _pageActive;
    }
}
