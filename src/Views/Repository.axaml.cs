using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.Repository repo && !string.IsNullOrWhiteSpace(repo.SearchCommitFilter))
                    repo.StartSearchCommits();

                e.Handled = true;
            }
        }

        private void OnSearchResultDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: Models.Commit commit } && DataContext is ViewModels.Repository repo)
            {
                repo.NavigateToCommit(commit.SHA);
            }

            e.Handled = true;
        }

        private void OnBranchTreeRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void OnLocalBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            RemoteBranchTree.UnselectAll();
            TagsList.SelectedItem = null;
        }

        private void OnRemoteBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            TagsList.SelectedItem = null;
        }

        private void OnTagDataGridSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            if (sender is DataGrid { SelectedItem: Models.Tag tag })
            {
                LocalBranchTree.UnselectAll();
                RemoteBranchTree.UnselectAll();

                if (DataContext is ViewModels.Repository repo)
                    repo.NavigateToCommit(tag.SHA);
            }
        }

        private void OnTagContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: Models.Tag tag } grid && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForTag(tag);
                grid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnTagFilterIsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton { DataContext: Models.Tag tag } toggle && DataContext is ViewModels.Repository repo)
            {
                repo.UpdateFilter(tag.Name, toggle.IsChecked == true);
            }

            e.Handled = true;
        }

        private void OnSubmoduleContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: string submodule } grid && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForSubmodule(submodule);
                grid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedSubmodule(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: string submodule } && DataContext is ViewModels.Repository repo)
            {
                repo.OpenSubmodule(submodule);
            }

            e.Handled = true;
        }

        private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: Models.Worktree worktree } grid && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForWorktree(worktree);
                grid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedWorktree(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: Models.Worktree worktree } && DataContext is ViewModels.Repository repo)
            {
                repo.OpenWorktree(worktree);
            }

            e.Handled = true;
        }

        private void OnLeftSidebarDataGridPropertyChanged(object _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataGrid.ItemsSourceProperty || e.Property == DataGrid.IsVisibleProperty)
            {
                UpdateLeftSidebarLayout();
            }
        }

        private void UpdateLeftSidebarLayout()
        {
            var vm = DataContext as ViewModels.Repository;
            if (vm == null || vm.Settings == null)
                return;

            if (!IsLoaded)
                return;

            var leftHeight = LeftSidebarGroups.Bounds.Height - 28.0 * 5;
            var localBranchRows = vm.IsLocalBranchGroupExpanded ? LocalBranchTree.Rows.Count : 0;
            var remoteBranchRows = vm.IsRemoteGroupExpanded ? RemoteBranchTree.Rows.Count : 0;
            var desiredBranches = (localBranchRows + remoteBranchRows) * 24.0;
            var desiredTag = vm.IsTagGroupExpanded ? TagsList.RowHeight * vm.VisibleTags.Count : 0;
            var desiredSubmodule = vm.IsSubmoduleGroupExpanded ? SubmoduleList.RowHeight * vm.Submodules.Count : 0;
            var desiredWorktree = vm.IsWorktreeGroupExpanded ? WorktreeList.RowHeight * vm.Worktrees.Count : 0;
            var desiredOthers = desiredTag + desiredSubmodule + desiredWorktree;
            var hasOverflow = (desiredBranches + desiredOthers > leftHeight);

            if (vm.IsTagGroupExpanded)
            {
                var height = desiredTag;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredSubmodule - desiredWorktree;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                TagsList.Height = height;
                hasOverflow = (desiredBranches + desiredSubmodule + desiredWorktree) > leftHeight;
            }

            if (vm.IsSubmoduleGroupExpanded)
            {
                var height = desiredSubmodule;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredWorktree;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                SubmoduleList.Height = height;
                hasOverflow = (desiredBranches + desiredWorktree) > leftHeight;
            }

            if (vm.IsWorktreeGroupExpanded)
            {
                var height = desiredWorktree;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                WorktreeList.Height = height;
            }

            if (desiredBranches > leftHeight)
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
    }
}
