using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Repository : UserControl
    {
        private const double CompactLayoutThreshold = 1100;
        private const double WorkspacePaneMinWidth = 300;
        private const double WorkspacePaneMinHeight = 160;
        private const double WorkspaceSplitterSize = 4;
        private bool _isCompactLayout;
        private bool _isCompactSidebarOpen;
        private GridLength _expandedSidebarWidth = new(260, GridUnitType.Pixel);
        private ViewModels.Repository _subscribedRepository;
        private int _activeWorkspaceViewIndex = -1;

        public Repository()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateLeftSidebarLayout();
            UpdateResponsiveLayout(Bounds.Width);

            if (DataContext is ViewModels.Repository repo)
            {
                SubscribeToRepository(repo);
                _activeWorkspaceViewIndex = repo.SelectedViewIndex;
            }

            ApplyWorkspaceLayout();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            UnsubscribeFromRepository();
            base.OnUnloaded(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            UnsubscribeFromRepository();

            if (IsLoaded && DataContext is ViewModels.Repository repo)
            {
                SubscribeToRepository(repo);
                _activeWorkspaceViewIndex = repo.SelectedViewIndex;
            }

            ApplyWorkspaceLayout();
        }

        private void OnRepositorySizeChanged(object _, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                UpdateResponsiveLayout(e.NewSize.Width);
        }

        private void UpdateResponsiveLayout(double width)
        {
            if (width <= 0)
                return;

            var sidebarColumn = RootLayout.ColumnDefinitions[0];
            var sidebarSplitterColumn = RootLayout.ColumnDefinitions[1];
            var useCompactLayout = width < CompactLayoutThreshold;
            if (useCompactLayout == _isCompactLayout)
            {
                if (_isCompactSidebarOpen)
                    FullSidebar.Width = Math.Min(340, Math.Max(240, width - 48));
                return;
            }

            if (useCompactLayout)
            {
                var current = ViewModels.Preferences.Instance.Layout.RepositorySidebarWidth;
                if (current.IsAbsolute && current.Value >= 200)
                    _expandedSidebarWidth = current;

                _isCompactLayout = true;
                _isCompactSidebarOpen = false;
                sidebarColumn.MinWidth = 0;
                sidebarColumn.MaxWidth = 48;
                sidebarColumn.SetCurrentValue(ColumnDefinition.WidthProperty, new GridLength(48, GridUnitType.Pixel));
                sidebarSplitterColumn.Width = new GridLength(0);
                SidebarSplitter.IsVisible = false;
                CompactNavigationRail.IsVisible = true;
                FullSidebar.IsVisible = false;
            }
            else
            {
                _isCompactLayout = false;
                _isCompactSidebarOpen = false;
                CompactSidebarBackdrop.IsVisible = false;
                CompactNavigationRail.IsVisible = false;
                CompactSidebarCloseButton.IsVisible = false;
                FullSidebar.IsVisible = true;
                FullSidebar.Width = double.NaN;
                FullSidebar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                Grid.SetColumnSpan(FullSidebar, 1);
                sidebarColumn.MinWidth = 200;
                sidebarColumn.MaxWidth = 500;
                sidebarColumn.SetCurrentValue(ColumnDefinition.WidthProperty, _expandedSidebarWidth);
                sidebarSplitterColumn.Width = new GridLength(3, GridUnitType.Pixel);
                SidebarSplitter.IsVisible = true;
                ViewModels.Preferences.Instance.Layout.RepositorySidebarWidth = _expandedSidebarWidth;
            }
        }

        private void OnOpenCompactSidebar(object _, RoutedEventArgs e)
        {
            if (_isCompactLayout)
            {
                _isCompactSidebarOpen = true;
                CompactSidebarBackdrop.IsVisible = true;
                CompactNavigationRail.IsVisible = false;
                CompactSidebarCloseButton.IsVisible = true;
                FullSidebar.IsVisible = true;
                FullSidebar.Width = Math.Min(340, Math.Max(240, Bounds.Width - 48));
                FullSidebar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                Grid.SetColumnSpan(FullSidebar, 3);
            }

            e.Handled = true;
        }

        private void OnCloseCompactSidebar(object _, RoutedEventArgs e)
        {
            CloseCompactSidebar();
            e.Handled = true;
        }

        private void OnCompactSidebarBackdropPressed(object _, PointerPressedEventArgs e)
        {
            CloseCompactSidebar();
            e.Handled = true;
        }

        private void OnCompactViewSelected(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string tag } &&
                int.TryParse(tag, out var selectedView) &&
                DataContext is ViewModels.Repository repo)
                repo.SelectedViewIndex = selectedView;

            CloseCompactSidebar();
            e.Handled = true;
        }

        private void OnRepositoryViewSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            CloseCompactSidebar();
            e.Handled = true;
        }

        private void CloseCompactSidebar()
        {
            if (!_isCompactLayout || !_isCompactSidebarOpen)
                return;

            _isCompactSidebarOpen = false;
            CompactSidebarBackdrop.IsVisible = false;
            CompactSidebarCloseButton.IsVisible = false;
            FullSidebar.IsVisible = false;
            FullSidebar.Width = double.NaN;
            Grid.SetColumnSpan(FullSidebar, 1);
            CompactNavigationRail.IsVisible = true;
        }

        private void SubscribeToRepository(ViewModels.Repository repo)
        {
            if (_subscribedRepository == repo)
                return;

            UnsubscribeFromRepository();
            _subscribedRepository = repo;
            _subscribedRepository.PropertyChanged += OnRepositoryPropertyChanged;
        }

        private void UnsubscribeFromRepository()
        {
            if (_subscribedRepository == null)
                return;

            _subscribedRepository.PropertyChanged -= OnRepositoryPropertyChanged;
            _subscribedRepository = null;
        }

        private void OnRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DataContext is not ViewModels.Repository repo)
                return;

            if (e.PropertyName == nameof(ViewModels.Repository.SelectedViewIndex))
                _activeWorkspaceViewIndex = repo.SelectedViewIndex;

            if (e.PropertyName is nameof(ViewModels.Repository.SelectedViewIndex) or
                nameof(ViewModels.Repository.SecondaryViewIndex) or
                nameof(ViewModels.Repository.IsSplitViewEnabled) or
                nameof(ViewModels.Repository.WorkspaceOrientation) or
                nameof(ViewModels.Repository.WorkspaceSplitRatio))
                ApplyWorkspaceLayout();
        }

        private void OnWorkspaceHostSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                ApplyWorkspaceLayout();
        }

        private void ApplyWorkspaceLayout()
        {
            if (DataContext is not ViewModels.Repository repo || WorkspaceHost.Bounds.Width <= 0)
                return;

            var pages = new[] { HistoriesPage, WorkingCopyPage, StashesPage };
            foreach (var page in pages)
            {
                page.IsVisible = false;
                Grid.SetColumn(page, 0);
                Grid.SetColumnSpan(page, 1);
                Grid.SetRow(page, 0);
                Grid.SetRowSpan(page, 1);
            }

            var columns = WorkspaceHost.ColumnDefinitions;
            var rows = WorkspaceHost.RowDefinitions;
            for (var i = 0; i < columns.Count; i++)
            {
                columns[i].MinWidth = 0;
                columns[i].Width = new GridLength(0);
            }
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].MinHeight = 0;
                rows[i].Height = new GridLength(0);
            }

            var primaryPage = GetWorkspacePage(repo.SelectedViewIndex);
            primaryPage.IsVisible = true;
            columns[0].Width = new GridLength(1, GridUnitType.Star);
            rows[0].Height = new GridLength(1, GridUnitType.Star);
            WorkspaceSplitter.IsVisible = false;

            if (repo.IsSplitViewEnabled)
            {
                var secondaryPage = GetWorkspacePage(repo.SecondaryViewIndex);
                secondaryPage.IsVisible = true;
                var ratio = repo.WorkspaceSplitRatio;
                var useSideBySide = repo.WorkspaceOrientation == Models.RepositoryWorkspaceOrientation.SideBySide &&
                    WorkspaceHost.Bounds.Width >= WorkspacePaneMinWidth * 2 + WorkspaceSplitterSize;

                WorkspaceSplitter.IsVisible = true;
                if (useSideBySide)
                {
                    columns[0].MinWidth = WorkspacePaneMinWidth;
                    columns[0].Width = new GridLength(ratio, GridUnitType.Star);
                    columns[1].Width = new GridLength(WorkspaceSplitterSize, GridUnitType.Pixel);
                    columns[2].MinWidth = WorkspacePaneMinWidth;
                    columns[2].Width = new GridLength(1 - ratio, GridUnitType.Star);

                    Grid.SetColumn(secondaryPage, 2);
                    Grid.SetColumn(WorkspaceSplitter, 1);
                    Grid.SetRow(WorkspaceSplitter, 0);
                    WorkspaceSplitter.Width = WorkspaceSplitterSize;
                    WorkspaceSplitter.Height = double.NaN;
                    WorkspaceSplitter.ResizeDirection = GridResizeDirection.Columns;
                    WorkspaceSplitter.BorderThickness = new Thickness(1, 0, 0, 0);
                }
                else
                {
                    rows[0].MinHeight = WorkspacePaneMinHeight;
                    rows[0].Height = new GridLength(ratio, GridUnitType.Star);
                    rows[1].Height = new GridLength(WorkspaceSplitterSize, GridUnitType.Pixel);
                    rows[2].MinHeight = WorkspacePaneMinHeight;
                    rows[2].Height = new GridLength(1 - ratio, GridUnitType.Star);

                    Grid.SetRow(secondaryPage, 2);
                    Grid.SetColumn(WorkspaceSplitter, 0);
                    Grid.SetRow(WorkspaceSplitter, 1);
                    WorkspaceSplitter.Width = double.NaN;
                    WorkspaceSplitter.Height = WorkspaceSplitterSize;
                    WorkspaceSplitter.ResizeDirection = GridResizeDirection.Rows;
                    WorkspaceSplitter.BorderThickness = new Thickness(0, 1, 0, 0);
                }
            }

            if (_activeWorkspaceViewIndex != repo.SelectedViewIndex &&
                _activeWorkspaceViewIndex != repo.SecondaryViewIndex)
                _activeWorkspaceViewIndex = repo.SelectedViewIndex;

            UpdateWorkspaceHotkeys();
        }

        private Border GetWorkspacePage(int viewIndex)
        {
            return viewIndex switch
            {
                1 => WorkingCopyPage,
                2 => StashesPage,
                _ => HistoriesPage,
            };
        }

        private int GetWorkspaceViewIndex(object page)
        {
            if (page == WorkingCopyPage)
                return 1;
            if (page == StashesPage)
                return 2;
            return 0;
        }

        private void OnWorkspacePagePointerEntered(object sender, PointerEventArgs e)
        {
            ActivateWorkspacePage(sender);
        }

        private void OnWorkspacePageGotFocus(object sender, GotFocusEventArgs e)
        {
            ActivateWorkspacePage(sender);
        }

        private void ActivateWorkspacePage(object page)
        {
            if (page is not Border { IsVisible: true } border)
                return;

            _activeWorkspaceViewIndex = GetWorkspaceViewIndex(border);
            UpdateWorkspaceHotkeys();
        }

        internal void UpdateWorkspaceHotkeys()
        {
            var pages = new[] { HistoriesPage, WorkingCopyPage, StashesPage };
            for (var i = 0; i < pages.Length; i++)
            {
                var diffViewer = pages[i].FindDescendantOfType<DiffView>();
                diffViewer?.ToggleHotkeyBindings(pages[i].IsVisible && i == _activeWorkspaceViewIndex);
            }
        }

        private void OnWorkspaceSplitterDragCompleted(object sender, VectorEventArgs e)
        {
            if (DataContext is not ViewModels.Repository { IsSplitViewEnabled: true } repo)
                return;

            var columns = WorkspaceHost.ColumnDefinitions;
            var rows = WorkspaceHost.RowDefinitions;
            double first;
            double second;
            if (WorkspaceSplitter.ResizeDirection == GridResizeDirection.Columns)
            {
                first = columns[0].ActualWidth;
                second = columns[2].ActualWidth;
            }
            else
            {
                first = rows[0].ActualHeight;
                second = rows[2].ActualHeight;
            }

            if (first + second > 0)
                repo.WorkspaceSplitRatio = first / (first + second);
        }

        private void OnRepositoryViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is not ViewModels.Repository { IsBare: false } repo ||
                sender is not Control control ||
                !int.TryParse(control.Tag?.ToString(), out var viewIndex))
                return;

            var menu = new ContextMenu();
            var openSecondary = new MenuItem
            {
                Header = App.Text("Repository.OpenInSecondary"),
                Icon = this.CreateMenuIcon("Icons.Layout"),
                IsEnabled = viewIndex != repo.SelectedViewIndex && viewIndex != repo.SecondaryViewIndex,
            };
            openSecondary.Click += (_, ev) =>
            {
                repo.OpenViewInSecondary(viewIndex, repo.WorkspaceOrientation);
                ev.Handled = true;
            };
            menu.Items.Add(openSecondary);
            menu.Items.Add(new MenuItem { Header = "-" });

            var canOpenSplit = repo.IsSplitViewEnabled || viewIndex != repo.SelectedViewIndex;
            var sideBySide = new MenuItem
            {
                Header = App.Text("Repository.SplitSideBySide"),
                Icon = repo.IsSplitViewEnabled && repo.WorkspaceOrientation == Models.RepositoryWorkspaceOrientation.SideBySide
                    ? this.CreateMenuIcon("Icons.Check")
                    : null,
                IsEnabled = canOpenSplit,
            };
            sideBySide.Click += (_, ev) =>
            {
                if (repo.IsSplitViewEnabled)
                    repo.SetWorkspaceOrientation(Models.RepositoryWorkspaceOrientation.SideBySide);
                else
                    repo.OpenViewInSecondary(viewIndex, Models.RepositoryWorkspaceOrientation.SideBySide);
                ev.Handled = true;
            };
            menu.Items.Add(sideBySide);

            var stacked = new MenuItem
            {
                Header = App.Text("Repository.SplitStacked"),
                Icon = repo.IsSplitViewEnabled && repo.WorkspaceOrientation == Models.RepositoryWorkspaceOrientation.Stacked
                    ? this.CreateMenuIcon("Icons.Check")
                    : null,
                IsEnabled = canOpenSplit,
            };
            stacked.Click += (_, ev) =>
            {
                if (repo.IsSplitViewEnabled)
                    repo.SetWorkspaceOrientation(Models.RepositoryWorkspaceOrientation.Stacked);
                else
                    repo.OpenViewInSecondary(viewIndex, Models.RepositoryWorkspaceOrientation.Stacked);
                ev.Handled = true;
            };
            menu.Items.Add(stacked);
            menu.Items.Add(new MenuItem { Header = "-" });

            var swap = new MenuItem
            {
                Header = App.Text("Repository.SwapViews"),
                Icon = this.CreateMenuIcon("Icons.Layout"),
                IsEnabled = repo.IsSplitViewEnabled,
            };
            swap.Click += (_, ev) =>
            {
                repo.SwapWorkspaceViews();
                ev.Handled = true;
            };
            menu.Items.Add(swap);

            var close = new MenuItem
            {
                Header = App.Text("Repository.CloseSecondaryView"),
                Icon = this.CreateMenuIcon("Icons.Close"),
                IsEnabled = repo.IsSplitViewEnabled,
            };
            close.Click += (_, ev) =>
            {
                repo.CloseSecondaryView();
                ev.Handled = true;
            };
            menu.Items.Add(close);
            menu.Open(control);
            e.Handled = true;
        }

        private void OnToggleFilter(object _, RoutedEventArgs e)
        {
            FilterBox.Focus();
            e.Handled = true;
        }

        private void OnSearchCommitPanelPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty && sender is Grid { IsVisible: true })
                TxtSearchCommitsBox.Focus();
        }

        private void OnSearchKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.Repository repo)
                return;

            if (e.Key == Key.Enter)
            {
                repo.SearchCommitContext.StartSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (repo.SearchCommitContext.Suggestions is { Count: > 0 })
                {
                    SearchSuggestionBox.Focus(NavigationMethod.Tab);
                    SearchSuggestionBox.SelectedIndex = 0;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                repo.SearchCommitContext.ClearSuggestions();
                e.Handled = true;
            }
        }

        private void OnClearSearchCommitFilter(object _, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.Repository repo)
                return;

            repo.SearchCommitContext.ClearFilter();
            e.Handled = true;
        }

        private void OnLocalBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            RemoteBranchTree.UnselectAll();
            TagsList.UnselectAll();
        }

        private void OnRemoteBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            TagsList.UnselectAll();
        }

        private void OnTagsSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            RemoteBranchTree.UnselectAll();
        }

        private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.Worktree worktree } ctrl && DataContext is ViewModels.Repository repo)
            {
                var menu = new ContextMenu();

                var switchTo = new MenuItem();
                switchTo.Header = App.Text("Worktree.Open");
                switchTo.Icon = this.CreateMenuIcon("Icons.Folder.Open");
                switchTo.Click += (_, ev) =>
                {
                    repo.OpenWorktree(worktree);
                    ev.Handled = true;
                };
                menu.Items.Add(switchTo);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (worktree.IsLocked)
                {
                    var unlock = new MenuItem();
                    unlock.Header = App.Text("Worktree.Unlock");
                    unlock.Icon = this.CreateMenuIcon("Icons.Unlock");
                    unlock.Click += async (_, ev) =>
                    {
                        await repo.UnlockWorktreeAsync(worktree);
                        ev.Handled = true;
                    };
                    menu.Items.Add(unlock);
                }
                else
                {
                    var loc = new MenuItem();
                    loc.Header = App.Text("Worktree.Lock");
                    loc.Icon = this.CreateMenuIcon("Icons.Lock");
                    loc.IsEnabled = !worktree.IsMain;
                    loc.Click += async (_, ev) =>
                    {
                        await repo.LockWorktreeAsync(worktree);
                        ev.Handled = true;
                    };
                    menu.Items.Add(loc);
                }

                var remove = new MenuItem();
                remove.Header = App.Text("Worktree.Remove");
                remove.Icon = this.CreateMenuIcon("Icons.Clear");
                remove.IsEnabled = !worktree.IsCurrent && !worktree.IsMain;
                remove.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.RemoveWorktree(repo, worktree));
                    ev.Handled = true;
                };
                menu.Items.Add(remove);

                var copy = new MenuItem();
                copy.Header = App.Text("Worktree.CopyPath");
                copy.Icon = this.CreateMenuIcon("Icons.Copy");
                copy.Click += async (_, ev) =>
                {
                    await this.CopyTextAsync(worktree.FullPath);
                    ev.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(copy);
                menu.Open(ctrl);
            }

            e.Handled = true;
        }

        private void OnWorktreeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.Worktree worktree } && DataContext is ViewModels.Repository repo)
                repo.OpenWorktree(worktree);

            e.Handled = true;
        }

        private void OnWorktreeListPropertyChanged(object _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ItemsControl.ItemsSourceProperty || e.Property == IsVisibleProperty)
                UpdateLeftSidebarLayout();
        }

        private void OnLeftSidebarRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void OnLeftSidebarSizeChanged(object _, SizeChangedEventArgs e)
        {
            if (e.HeightChanged)
                UpdateLeftSidebarLayout();
        }

        private void UpdateLeftSidebarLayout()
        {
            var vm = DataContext as ViewModels.Repository;
            if (vm?.Settings == null)
                return;

            if (!IsLoaded)
                return;

            var leftHeight = LeftSidebarGroups.Bounds.Height - 28.0 * 5 - 4;
            if (leftHeight <= 0)
                return;

            var localBranchRows = vm.IsLocalBranchGroupExpanded ? LocalBranchTree.Rows.Count : 0;
            var remoteBranchRows = vm.IsRemoteGroupExpanded ? RemoteBranchTree.Rows.Count : 0;
            var desiredBranches = (localBranchRows + remoteBranchRows) * 24.0;
            var desiredTag = vm.IsTagGroupExpanded ? 24.0 * TagsList.Rows : 0;
            var desiredSubmodule = vm.IsSubmoduleGroupExpanded ? 24.0 * SubmoduleList.Rows : 0;
            var desiredWorktree = vm.IsWorktreeGroupExpanded ? 24.0 * vm.Worktrees.Count : 0;
            var desiredOthers = desiredTag + desiredSubmodule + desiredWorktree;
            var hasOverflow = (desiredBranches + desiredOthers > leftHeight);

            if (vm.IsWorktreeGroupExpanded)
            {
                var height = desiredWorktree;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredTag - desiredSubmodule;
                    if (test < 0)
                        height = Math.Min(120, height);
                    else
                        height = Math.Max(120, test);
                }

                leftHeight -= height;
                WorktreeList.Height = height;
                hasOverflow = (desiredBranches + desiredTag + desiredSubmodule) > leftHeight;
            }

            if (vm.IsSubmoduleGroupExpanded)
            {
                var height = desiredSubmodule;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredTag;
                    if (test < 0)
                        height = Math.Min(120, height);
                    else
                        height = Math.Max(120, test);
                }

                leftHeight -= height;
                SubmoduleList.Height = height;
                hasOverflow = (desiredBranches + desiredTag) > leftHeight;
            }

            if (vm.IsTagGroupExpanded)
            {
                var height = desiredTag;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches;
                    if (test < 0)
                        height = Math.Min(120, height);
                    else
                        height = Math.Max(120, test);
                }

                leftHeight -= height;
                TagsList.Height = height;
            }

            if (leftHeight > 0 && desiredBranches > leftHeight)
            {
                var local = localBranchRows * 24.0;
                var remote = remoteBranchRows * 24.0;
                var half = leftHeight / 2;
                if (vm.IsLocalBranchGroupExpanded)
                {
                    if (vm.IsRemoteGroupExpanded)
                    {
                        if (local < half)
                        {
                            LocalBranchTree.Height = local;
                            RemoteBranchTree.Height = leftHeight - local;
                        }
                        else if (remote < half)
                        {
                            RemoteBranchTree.Height = remote;
                            LocalBranchTree.Height = leftHeight - remote;
                        }
                        else
                        {
                            LocalBranchTree.Height = half;
                            RemoteBranchTree.Height = half;
                        }
                    }
                    else
                    {
                        LocalBranchTree.Height = leftHeight;
                    }
                }
                else if (vm.IsRemoteGroupExpanded)
                {
                    RemoteBranchTree.Height = leftHeight;
                }
            }
            else
            {
                if (vm.IsLocalBranchGroupExpanded)
                {
                    var height = localBranchRows * 24;
                    LocalBranchTree.Height = height;
                }

                if (vm.IsRemoteGroupExpanded)
                {
                    var height = remoteBranchRows * 24;
                    RemoteBranchTree.Height = height;
                }
            }
        }

        private void OnSearchSuggestionBoxKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.Repository repo)
                return;

            if (e.Key == Key.Escape)
            {
                repo.SearchCommitContext.ClearSuggestions();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                var selected = SearchSuggestionBox.SelectedItem;
                if (selected is string content)
                {
                    repo.SearchCommitContext.Filter = content;
                    TxtSearchCommitsBox.CaretIndex = content.Length;
                }
                else if (selected is Models.User user)
                {
                    var apply = user.ToString().EscapeForBRE();
                    repo.SearchCommitContext.Filter = apply;
                    TxtSearchCommitsBox.CaretIndex = apply.Length;
                }

                repo.SearchCommitContext.StartSearch();
                e.Handled = true;
            }
        }

        private void OnSearchSuggestionTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.Repository repo)
                return;

            var ctx = (sender as Control)?.DataContext;
            if (ctx is string content)
            {
                repo.SearchCommitContext.Filter = content;
                TxtSearchCommitsBox.CaretIndex = content.Length;
            }
            else if (ctx is Models.User user)
            {
                var apply = user.ToString().EscapeForBRE();
                repo.SearchCommitContext.Filter = apply;
                TxtSearchCommitsBox.CaretIndex = apply.Length;
            }

            repo.SearchCommitContext.StartSearch();
            e.Handled = true;
        }

        private void OnOpenAdvancedHistoriesOption(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository { Histories: { } histories } repo)
            {
                var pref = ViewModels.Preferences.Instance;

                var layout = new MenuItem();
                layout.Header = App.Text("Repository.HistoriesLayout");
                layout.IsEnabled = false;

                var isHorizontal = pref.UseTwoColumnsLayoutInHistories;
                var horizontal = new MenuItem();
                horizontal.Header = App.Text("Repository.HistoriesLayout.Horizontal");
                if (isHorizontal)
                    horizontal.Icon = this.CreateMenuIcon("Icons.Check");
                horizontal.Click += (_, ev) =>
                {
                    pref.UseTwoColumnsLayoutInHistories = true;
                    ev.Handled = true;
                };

                var vertical = new MenuItem();
                vertical.Header = App.Text("Repository.HistoriesLayout.Vertical");
                if (!isHorizontal)
                    vertical.Icon = this.CreateMenuIcon("Icons.Check");
                vertical.Click += (_, ev) =>
                {
                    pref.UseTwoColumnsLayoutInHistories = false;
                    ev.Handled = true;
                };

                var showFlags = new MenuItem();
                showFlags.Header = App.Text("Repository.ShowFlags");
                showFlags.IsEnabled = false;

                var reflog = new MenuItem();
                reflog.Header = App.Text("Repository.ShowLostCommits");
                reflog.Tag = "--reflog";
                if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.Reflog))
                    reflog.Icon = this.CreateMenuIcon("Icons.Check");
                reflog.Click += (_, ev) =>
                {
                    repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.Reflog);
                    ev.Handled = true;
                };

                var firstParentOnly = new MenuItem();
                firstParentOnly.Header = App.Text("Repository.ShowFirstParentOnly");
                firstParentOnly.Tag = "--first-parent";
                if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.FirstParentOnly))
                    firstParentOnly.Icon = this.CreateMenuIcon("Icons.Check");
                firstParentOnly.Click += (_, ev) =>
                {
                    repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.FirstParentOnly);
                    ev.Handled = true;
                };

                var simplifyByDecoration = new MenuItem();
                simplifyByDecoration.Header = App.Text("Repository.ShowDecoratedCommitsOnly");
                simplifyByDecoration.Tag = "--simplify-by-decoration";
                if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.SimplifyByDecoration))
                    simplifyByDecoration.Icon = this.CreateMenuIcon("Icons.Check");
                simplifyByDecoration.Click += (_, ev) =>
                {
                    repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.SimplifyByDecoration);
                    ev.Handled = true;
                };

                var order = new MenuItem();
                order.Header = App.Text("Repository.HistoriesOrder");
                order.IsEnabled = false;

                var dateOrder = new MenuItem();
                dateOrder.Header = App.Text("Repository.HistoriesOrder.ByDate");
                dateOrder.Tag = "--date-order";
                if (!repo.EnableTopoOrderInHistory)
                    dateOrder.Icon = this.CreateMenuIcon("Icons.Check");
                dateOrder.Click += (_, ev) =>
                {
                    repo.EnableTopoOrderInHistory = false;
                    ev.Handled = true;
                };

                var topoOrder = new MenuItem();
                topoOrder.Header = App.Text("Repository.HistoriesOrder.Topo");
                topoOrder.Tag = "--topo-order";
                if (repo.EnableTopoOrderInHistory)
                    topoOrder.Icon = this.CreateMenuIcon("Icons.Check");
                topoOrder.Click += (_, ev) =>
                {
                    repo.EnableTopoOrderInHistory = true;
                    ev.Handled = true;
                };

                var highlights = new MenuItem();
                highlights.Header = App.Text("Histories.HighlightsInGraph");
                highlights.IsEnabled = false;

                var all = new MenuItem();
                all.Header = App.Text("Histories.HighlightsInGraph.All");
                if (histories.GraphHighlighting == Models.CommitGraphHighlighting.All)
                    all.Icon = this.CreateMenuIcon("Icons.Check");
                all.Click += (_, ev) =>
                {
                    histories.GraphHighlighting = Models.CommitGraphHighlighting.All;
                    ev.Handled = true;
                };

                var currentBranchOnly = new MenuItem();
                currentBranchOnly.Header = App.Text("Histories.HighlightsInGraph.CurrentBranchOnly");
                if (histories.GraphHighlighting == Models.CommitGraphHighlighting.CurrentBranchOnly)
                    currentBranchOnly.Icon = this.CreateMenuIcon("Icons.Check");
                currentBranchOnly.Click += (_, ev) =>
                {
                    histories.GraphHighlighting = Models.CommitGraphHighlighting.CurrentBranchOnly;
                    ev.Handled = true;
                };

                var selectedCommitsOnly = new MenuItem();
                selectedCommitsOnly.Header = App.Text("Histories.HighlightsInGraph.SelectedCommitsOnly");
                if (histories.GraphHighlighting == Models.CommitGraphHighlighting.SelectedCommitsOnly)
                    selectedCommitsOnly.Icon = this.CreateMenuIcon("Icons.Check");
                selectedCommitsOnly.Click += (_, ev) =>
                {
                    histories.GraphHighlighting = Models.CommitGraphHighlighting.SelectedCommitsOnly;
                    ev.Handled = true;
                };

                var selectedCommitsOnlyFirstParent = new MenuItem();
                selectedCommitsOnlyFirstParent.Header = App.Text("Histories.HighlightsInGraph.SelectedCommitsOnlyFirstParent");
                if (histories.GraphHighlighting == Models.CommitGraphHighlighting.SelectedCommitsOnlyFirstParent)
                    selectedCommitsOnlyFirstParent.Icon = this.CreateMenuIcon("Icons.Check");
                selectedCommitsOnlyFirstParent.Click += (_, ev) =>
                {
                    histories.GraphHighlighting = Models.CommitGraphHighlighting.SelectedCommitsOnlyFirstParent;
                    ev.Handled = true;
                };

                var currentBranchAndSelectedCommits = new MenuItem();
                currentBranchAndSelectedCommits.Header = App.Text("Histories.HighlightsInGraph.CurrentBranchAndSelectedCommits");
                if (histories.GraphHighlighting == Models.CommitGraphHighlighting.CurrentBranchAndSelectedCommits)
                    currentBranchAndSelectedCommits.Icon = this.CreateMenuIcon("Icons.Check");
                currentBranchAndSelectedCommits.Click += (_, ev) =>
                {
                    histories.GraphHighlighting = Models.CommitGraphHighlighting.CurrentBranchAndSelectedCommits;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(layout);
                menu.Items.Add(horizontal);
                menu.Items.Add(vertical);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(showFlags);
                menu.Items.Add(reflog);
                menu.Items.Add(firstParentOnly);
                menu.Items.Add(simplifyByDecoration);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(order);
                menu.Items.Add(dateOrder);
                menu.Items.Add(topoOrder);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(highlights);
                menu.Items.Add(all);
                menu.Items.Add(currentBranchOnly);
                menu.Items.Add(selectedCommitsOnly);
                menu.Items.Add(selectedCommitsOnlyFirstParent);
                menu.Items.Add(currentBranchAndSelectedCommits);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenSortLocalBranchMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var isSortByName = repo.IsSortingLocalBranchByName;
                var byNameAsc = new MenuItem();
                byNameAsc.Header = App.Text("Repository.BranchSort.ByName");
                if (isSortByName)
                    byNameAsc.Icon = this.CreateMenuIcon("Icons.Check");
                byNameAsc.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingLocalBranchByName = true;
                    ev.Handled = true;
                };

                var byCommitterDate = new MenuItem();
                byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
                if (!isSortByName)
                    byCommitterDate.Icon = this.CreateMenuIcon("Icons.Check");
                byCommitterDate.Click += (_, ev) =>
                {
                    if (isSortByName)
                        repo.IsSortingLocalBranchByName = false;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(byNameAsc);
                menu.Items.Add(byCommitterDate);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenSortRemoteBranchMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var isSortByName = repo.IsSortingRemoteBranchByName;
                var byNameAsc = new MenuItem();
                byNameAsc.Header = App.Text("Repository.BranchSort.ByName");
                if (isSortByName)
                    byNameAsc.Icon = this.CreateMenuIcon("Icons.Check");
                byNameAsc.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingRemoteBranchByName = true;
                    ev.Handled = true;
                };

                var byCommitterDate = new MenuItem();
                byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
                if (!isSortByName)
                    byCommitterDate.Icon = this.CreateMenuIcon("Icons.Check");
                byCommitterDate.Click += (_, ev) =>
                {
                    if (isSortByName)
                        repo.IsSortingRemoteBranchByName = false;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(byNameAsc);
                menu.Items.Add(byCommitterDate);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenSortTagMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var isSortByName = repo.IsSortingTagsByName;
                var byCreatorDate = new MenuItem();
                byCreatorDate.Header = App.Text("Repository.Tags.OrderByCreatorDate");
                if (!isSortByName)
                    byCreatorDate.Icon = this.CreateMenuIcon("Icons.Check");
                byCreatorDate.Click += (_, ev) =>
                {
                    if (isSortByName)
                        repo.IsSortingTagsByName = false;
                    ev.Handled = true;
                };

                var byName = new MenuItem();
                byName.Header = App.Text("Repository.Tags.OrderByName");
                if (isSortByName)
                    byName.Icon = this.CreateMenuIcon("Icons.Check");
                byName.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingTagsByName = true;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(byName);
                menu.Items.Add(byCreatorDate);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private async void OnPruneWorktrees(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.PruneWorktreesAsync();

            e.Handled = true;
        }

        private async void OnSkipInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.SkipMergeAsync();

            e.Handled = true;
        }

        private void OnResolveInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                repo.SelectedViewIndex = 1;

            e.Handled = true;
        }

        private async void OnAbortInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.AbortMergeAsync();

            e.Handled = true;
        }

        private void OnRemoveSelectedHistoryFilter(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && sender is Button { DataContext: Models.HistoryFilter filter })
                repo.RemoveHistoryFilter(filter);

            e.Handled = true;
        }

        private async void OnBisectCommand(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                DataContext is ViewModels.Repository { IsBisectCommandRunning: false } repo &&
                repo.CanCreatePopup())
                await repo.ExecBisectCommandAsync(button.Tag as string);

            e.Handled = true;
        }

    }
}
