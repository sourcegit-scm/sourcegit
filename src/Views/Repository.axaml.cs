using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Utils;

namespace SourceGit.Views
{
    public partial class Repository : UserControl
    {
        public Repository()
        {
            InitializeComponent();
        }

        private void OpenWithExternalTools(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForExternalTools();
                button.OpenContextMenu(menu);
                e.Handled = true;
            }
        }

        private void OpenGitFlowMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForGitFlow();
                (sender as Control)?.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OpenGitLFSMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForGitLFS();
                (sender as Control)?.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private async void OpenStatistics(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var dialog = new Statistics() { DataContext = new ViewModels.Statistics(repo.FullPath) };
                await dialog.ShowDialog(TopLevel.GetTopLevel(this) as Window);
                e.Handled = true;
            }
        }

        private void OnSearchCommitPanelPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            var grid = sender as Grid;
            if (e.Property == IsVisibleProperty && grid.IsVisible)
                txtSearchCommitsBox.Focus();
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.Repository repo)
                    repo.StartSearchCommits();

                e.Handled = true;
            }
        }

        private void OnSearchResultDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null)
            {
                if (DataContext is ViewModels.Repository repo)
                {
                    var commit = datagrid.SelectedItem as Models.Commit;
                    repo.NavigateToCommit(commit.SHA);
                }
            }
            e.Handled = true;
        }

        private void OnLocalBranchTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TreeView tree && tree.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                remoteBranchTree.UnselectAll();
                tagsList.SelectedItem = null;

                ViewModels.BranchTreeNode prev = null;
                foreach (var node in repo.LocalBranchTrees)
                    node.UpdateCornerRadius(ref prev);

                if (tree.SelectedItems.Count == 1)
                {
                    var node = tree.SelectedItem as ViewModels.BranchTreeNode;
                    if (node.IsBranch)
                        repo.NavigateToCommit((node.Backend as Models.Branch).Head);
                }
            }
        }

        private void OnRemoteBranchTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TreeView tree && tree.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                localBranchTree.UnselectAll();
                tagsList.SelectedItem = null;

                ViewModels.BranchTreeNode prev = null;
                foreach (var node in repo.RemoteBranchTrees)
                    node.UpdateCornerRadius(ref prev);

                if (tree.SelectedItems.Count == 1)
                {
                    var node = tree.SelectedItem as ViewModels.BranchTreeNode;
                    if (node.IsBranch)
                        repo.NavigateToCommit((node.Backend as Models.Branch).Head);
                }
            }
        }

        private void OnLocalBranchContextMenuRequested(object sender, ContextRequestedEventArgs e)
        {
            remoteBranchTree.UnselectAll();
            tagsList.SelectedItem = null;

            var repo = DataContext as ViewModels.Repository;
            var tree = sender as TreeView;
            if (tree.SelectedItems.Count == 0)
            {
                e.Handled = true;
                return;
            }

            var branches = new List<Models.Branch>();
            foreach (var item in tree.SelectedItems)
                CollectBranchesFromNode(branches, item as ViewModels.BranchTreeNode);

            if (branches.Count == 1)
            {
                var item = (e.Source as Control)?.FindAncestorOfType<TreeViewItem>(true);
                if (item != null)
                {
                    var menu = repo.CreateContextMenuForLocalBranch(branches[0]);
                    item.OpenContextMenu(menu);
                }
            }
            else if (branches.Count > 1 && branches.Find(x => x.IsCurrent) == null)
            {
                var menu = new ContextMenu();
                var deleteMulti = new MenuItem();
                deleteMulti.Header = App.Text("BranchCM.DeleteMultiBranches", branches.Count);
                deleteMulti.Icon = App.CreateMenuIcon("Icons.Clear");
                deleteMulti.Click += (_, ev) =>
                {
                    repo.DeleteMultipleBranches(branches, true);
                    ev.Handled = true;
                };
                menu.Items.Add(deleteMulti);
                tree.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnRemoteBranchContextMenuRequested(object sender, ContextRequestedEventArgs e)
        {
            localBranchTree.UnselectAll();
            tagsList.SelectedItem = null;

            var repo = DataContext as ViewModels.Repository;
            var tree = sender as TreeView;
            if (tree.SelectedItems.Count == 0)
            {
                e.Handled = true;
                return;
            }

            if (tree.SelectedItems.Count == 1)
            {
                var node = tree.SelectedItem as ViewModels.BranchTreeNode;
                if (node != null && node.IsRemote)
                {
                    var item = (e.Source as Control)?.FindAncestorOfType<TreeViewItem>(true);
                    if (item != null && item.DataContext == node)
                    {
                        var menu = repo.CreateContextMenuForRemote(node.Backend as Models.Remote);
                        item.OpenContextMenu(menu);
                    }

                    e.Handled = true;
                    return;
                }
            }

            var branches = new List<Models.Branch>();
            foreach (var item in tree.SelectedItems)
                CollectBranchesFromNode(branches, item as ViewModels.BranchTreeNode);

            if (branches.Count == 1)
            {
                var item = (e.Source as Control)?.FindAncestorOfType<TreeViewItem>(true);
                if (item != null)
                {
                    var menu = repo.CreateContextMenuForRemoteBranch(branches[0]);
                    item.OpenContextMenu(menu);
                }
            }
            else if (branches.Count > 1)
            {
                var menu = new ContextMenu();
                var deleteMulti = new MenuItem();
                deleteMulti.Header = App.Text("BranchCM.DeleteMultiBranches", branches.Count);
                deleteMulti.Icon = App.CreateMenuIcon("Icons.Clear");
                deleteMulti.Click += (_, ev) =>
                {
                    repo.DeleteMultipleBranches(branches, false);
                    ev.Handled = true;
                };
                menu.Items.Add(deleteMulti);
                tree.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedBranchNode(object sender, TappedEventArgs e)
        {
            if (!ViewModels.PopupHost.CanCreatePopup())
                return;

            if (sender is Grid grid && DataContext is ViewModels.Repository repo)
            {
                var node = grid.DataContext as ViewModels.BranchTreeNode;
                if (node != null && node.IsBranch)
                {
                    var branch = node.Backend as Models.Branch;
                    if (branch.IsCurrent)
                        return;

                    repo.CheckoutBranch(branch);
                    e.Handled = true;
                }
            }
        }

        private void OnTagDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null)
            {
                localBranchTree.UnselectAll();
                remoteBranchTree.UnselectAll();

                var tag = datagrid.SelectedItem as Models.Tag;
                if (DataContext is ViewModels.Repository repo)
                    repo.NavigateToCommit(tag.SHA);
            }
        }

        private void OnTagContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var tag = datagrid.SelectedItem as Models.Tag;
                var menu = repo.CreateContextMenuForTag(tag);
                datagrid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnToggleFilter(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                var filter = string.Empty;
                if (toggle.DataContext is ViewModels.BranchTreeNode node)
                {
                    if (node.IsBranch)
                        filter = (node.Backend as Models.Branch).FullName;
                }
                else if (toggle.DataContext is Models.Tag tag)
                {
                    filter = tag.Name;
                }

                if (!string.IsNullOrEmpty(filter) && DataContext is ViewModels.Repository repo)
                {
                    repo.UpdateFilter(filter, toggle.IsChecked == true);
                }
            }

            e.Handled = true;
        }

        private void OnSubmoduleContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var submodule = datagrid.SelectedItem as string;
                var menu = repo.CreateContextMenuForSubmodule(submodule);
                datagrid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedSubmodule(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var submodule = datagrid.SelectedItem as string;
                (DataContext as ViewModels.Repository).OpenSubmodule(submodule);
            }

            e.Handled = true;
        }

        private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var worktree = datagrid.SelectedItem as Models.Worktree;
                var menu = repo.CreateContextMenuForWorktree(worktree);
                datagrid.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedWorktree(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var worktree = datagrid.SelectedItem as Models.Worktree;
                (DataContext as ViewModels.Repository).OpenWorktree(worktree);
            }

            e.Handled = true;
        }

        private void CollectBranchesFromNode(List<Models.Branch> outs, ViewModels.BranchTreeNode node)
        {
            if (node == null || node.IsRemote)
                return;

            if (node.IsFolder)
            {
                foreach (var child in node.Children)
                    CollectBranchesFromNode(outs, child);
            }
            else
            {
                var b = node.Backend as Models.Branch;
                if (b != null && !outs.Contains(b))
                    outs.Add(b);
            }
        }

        private void OnDashboardLayoutUpdated(object sender, EventArgs e) 
        {
            var vm = DataContext as ViewModels.Repository;
            if (vm == null || vm.Settings == null)
                return;

            var grid = sender as Grid;
            if (grid == null)
                return;

            var leftHeight = grid.Bounds.Height - 28.0 * 5;
            if (vm.IsTagGroupExpanded)
            {
                var desiredHeight = Math.Min(200.0, tagsList.RowHeight * vm.VisibleTags.Count);
                leftHeight -= desiredHeight;
                if (!tagsList.Height.IsClose(desiredHeight))
                    tagsList.Height = desiredHeight;
            }

            if (vm.IsSubmoduleGroupExpanded)
            {
                var desiredHeight = Math.Min(200.0, submoduleList.RowHeight * vm.Submodules.Count);
                leftHeight -= desiredHeight;
                if (!submoduleList.Height.IsClose(desiredHeight))
                    submoduleList.Height = desiredHeight;
            }

            if (vm.IsWorktreeGroupExpanded)
            {
                var desiredHeight = Math.Min(200.0, worktreeList.RowHeight * vm.Worktrees.Count);
                leftHeight -= desiredHeight;
                if (!worktreeList.Height.IsClose(desiredHeight))
                    worktreeList.Height = desiredHeight;
            }

            var desiredLocalBranchHeight = GetTreeRowsCount(vm.LocalBranchTrees) * 24;
            var desiredRemoteHeight = GetTreeRowsCount(vm.RemoteBranchTrees) * 24;

            if (!vm.IsRemoteGroupExpanded)
            {
                if (vm.IsLocalBranchGroupExpanded)
                {
                    var localBranchHeight = Math.Min(leftHeight, desiredLocalBranchHeight);
                    localBranchTree.Height = localBranchHeight;
                }
            }
            else
            {
                if (vm.IsLocalBranchGroupExpanded)
                {
                    var localBranchHeight = Math.Min(leftHeight * 0.5, desiredLocalBranchHeight);
                    if (!localBranchTree.Height.IsClose(localBranchHeight))
                        localBranchTree.Height = localBranchHeight;

                    leftHeight -= localBranchHeight;
                }

                var remoteHeight = Math.Min(leftHeight, desiredRemoteHeight);
                if (!remoteBranchTree.Height.IsClose(remoteHeight))
                    remoteBranchTree.Height = remoteHeight;
            }
        }

        private int GetTreeRowsCount(List<ViewModels.BranchTreeNode> nodes)
        {
            int count = nodes.Count;

            foreach (var node in nodes)
            {
                if (!node.IsBranch && node.IsExpanded)
                    count += GetTreeRowsCount(node.Children);
            }
                
            return count;
        }
    }
}
