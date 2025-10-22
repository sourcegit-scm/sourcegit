using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Repository : UserControl
    {
        public Repository()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateLeftSidebarLayout();
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
                if (!string.IsNullOrWhiteSpace(repo.SearchCommitFilter))
                    repo.StartSearchCommits();

                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (repo.MatchedFilesForSearching is { Count: > 0 })
                {
                    SearchSuggestionBox.Focus(NavigationMethod.Tab);
                    SearchSuggestionBox.SelectedIndex = 0;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                repo.ClearMatchedFilesForSearching();
                e.Handled = true;
            }
        }

        private void OnBranchTreeRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
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

        private void OnTagsRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void OnTagsSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            RemoteBranchTree.UnselectAll();
        }

        private void OnSubmodulesRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: Models.Worktree worktree } grid && DataContext is ViewModels.Repository repo)
            {
                var menu = new ContextMenu();

                var switchTo = new MenuItem();
                switchTo.Header = App.Text("Worktree.Open");
                switchTo.Icon = App.CreateMenuIcon("Icons.Folder.Open");
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
                    unlock.Icon = App.CreateMenuIcon("Icons.Unlock");
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
                    loc.Icon = App.CreateMenuIcon("Icons.Lock");
                    loc.Click += async (_, ev) =>
                    {
                        await repo.LockWorktreeAsync(worktree);
                        ev.Handled = true;
                    };
                    menu.Items.Add(loc);
                }

                var remove = new MenuItem();
                remove.Header = App.Text("Worktree.Remove");
                remove.Icon = App.CreateMenuIcon("Icons.Clear");
                remove.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.RemoveWorktree(repo, worktree));
                    ev.Handled = true;
                };
                menu.Items.Add(remove);

                var copy = new MenuItem();
                copy.Header = App.Text("Worktree.CopyPath");
                copy.Icon = App.CreateMenuIcon("Icons.Copy");
                copy.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(worktree.FullPath);
                    ev.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(copy);
                menu.Open(grid);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedWorktree(object sender, TappedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: Models.Worktree worktree } && DataContext is ViewModels.Repository repo)
                repo.OpenWorktree(worktree);

            e.Handled = true;
        }

        private void OnWorktreeListPropertyChanged(object _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ItemsControl.ItemsSourceProperty || e.Property == IsVisibleProperty)
                UpdateLeftSidebarLayout();
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
                repo.ClearMatchedFilesForSearching();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && SearchSuggestionBox.SelectedItem is string content)
            {
                repo.SearchCommitFilter = content;
                TxtSearchCommitsBox.CaretIndex = content.Length;
                repo.StartSearchCommits();
                e.Handled = true;
            }
        }

        private void OnSearchSuggestionDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.Repository repo)
                return;

            var content = (sender as StackPanel)?.DataContext as string;
            if (!string.IsNullOrEmpty(content))
            {
                repo.SearchCommitFilter = content;
                TxtSearchCommitsBox.CaretIndex = content.Length;
                repo.StartSearchCommits();
            }
            e.Handled = true;
        }

        private void OnOpenAdvancedHistoriesOption(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var pref = ViewModels.Preferences.Instance;

                var layout = new MenuItem();
                layout.Header = App.Text("Repository.HistoriesLayout");
                layout.IsEnabled = false;

                var isHorizontal = pref.UseTwoColumnsLayoutInHistories;
                var horizontal = new MenuItem();
                horizontal.Header = App.Text("Repository.HistoriesLayout.Horizontal");
                if (isHorizontal)
                    horizontal.Icon = App.CreateMenuIcon("Icons.Check");
                horizontal.Click += (_, ev) =>
                {
                    pref.UseTwoColumnsLayoutInHistories = true;
                    ev.Handled = true;
                };

                var vertical = new MenuItem();
                vertical.Header = App.Text("Repository.HistoriesLayout.Vertical");
                if (!isHorizontal)
                    vertical.Icon = App.CreateMenuIcon("Icons.Check");
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
                    reflog.Icon = App.CreateMenuIcon("Icons.Check");
                reflog.Click += (_, ev) =>
                {
                    repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.Reflog);
                    ev.Handled = true;
                };

                var firstParentOnly = new MenuItem();
                firstParentOnly.Header = App.Text("Repository.ShowFirstParentOnly");
                firstParentOnly.Tag = "--first-parent";
                if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.FirstParentOnly))
                    firstParentOnly.Icon = App.CreateMenuIcon("Icons.Check");
                firstParentOnly.Click += (_, ev) =>
                {
                    repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.FirstParentOnly);
                    ev.Handled = true;
                };

                var simplifyByDecoration = new MenuItem();
                simplifyByDecoration.Header = App.Text("Repository.ShowDecoratedCommitsOnly");
                simplifyByDecoration.Tag = "--simplify-by-decoration";
                if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.SimplifyByDecoration))
                    simplifyByDecoration.Icon = App.CreateMenuIcon("Icons.Check");
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
                if (!repo.EnableTopoOrderInHistories)
                    dateOrder.Icon = App.CreateMenuIcon("Icons.Check");
                dateOrder.Click += (_, ev) =>
                {
                    repo.EnableTopoOrderInHistories = false;
                    ev.Handled = true;
                };

                var topoOrder = new MenuItem();
                topoOrder.Header = App.Text("Repository.HistoriesOrder.Topo");
                topoOrder.Tag = "--topo-order";
                if (repo.EnableTopoOrderInHistories)
                    topoOrder.Icon = App.CreateMenuIcon("Icons.Check");
                topoOrder.Click += (_, ev) =>
                {
                    repo.EnableTopoOrderInHistories = true;
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
                    byNameAsc.Icon = App.CreateMenuIcon("Icons.Check");
                byNameAsc.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingLocalBranchByName = true;
                    ev.Handled = true;
                };

                var byCommitterDate = new MenuItem();
                byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
                if (!isSortByName)
                    byCommitterDate.Icon = App.CreateMenuIcon("Icons.Check");
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
                    byNameAsc.Icon = App.CreateMenuIcon("Icons.Check");
                byNameAsc.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingRemoteBranchByName = true;
                    ev.Handled = true;
                };

                var byCommitterDate = new MenuItem();
                byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
                if (!isSortByName)
                    byCommitterDate.Icon = App.CreateMenuIcon("Icons.Check");
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
                    byCreatorDate.Icon = App.CreateMenuIcon("Icons.Check");
                byCreatorDate.Click += (_, ev) =>
                {
                    if (isSortByName)
                        repo.IsSortingTagsByName = false;
                    ev.Handled = true;
                };

                var byName = new MenuItem();
                byName.Header = App.Text("Repository.Tags.OrderByName");
                if (isSortByName)
                    byName.Icon = App.CreateMenuIcon("Icons.Check");
                byName.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingTagsByName = true;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(byCreatorDate);
                menu.Items.Add(byName);
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

        private async void OnAbortInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.AbortMergeAsync();

            e.Handled = true;
        }

        private void OnRemoveSelectedHistoriesFilter(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && sender is Button { DataContext: Models.Filter filter })
                repo.RemoveHistoriesFilter(filter);

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
