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

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is ViewModels.Repository repo && !repo.IsSearching)
            {
                UpdateLeftSidebarLayout();
            }            
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
                if (node == null)
                    return;

                if (node.IsBranch)
                {
                    var branch = node.Backend as Models.Branch;
                    if (branch.IsCurrent)
                        return;

                    repo.CheckoutBranch(branch);
                }
                else
                {
                    node.IsExpanded = !node.IsExpanded;
                    UpdateLeftSidebarLayout();
                }

                e.Handled = true;
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

        private void OnLeftSidebarTreeViewPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TreeView.ItemsSourceProperty || e.Property == TreeView.IsVisibleProperty)
            {
                UpdateLeftSidebarLayout();
            }
        }

        private void OnLeftSidebarDataGridPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
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

            var leftHeight = leftSidebarGroups.Bounds.Height - 28.0 * 5;
            var localBranchRows = vm.IsLocalBranchGroupExpanded ? GetTreeRowsCount(vm.LocalBranchTrees) : 0;
            var remoteBranchRows = vm.IsRemoteGroupExpanded ? GetTreeRowsCount(vm.RemoteBranchTrees) : 0;
            var desiredBranches = (localBranchRows + remoteBranchRows) * 24.0;
            var desiredTag = vm.IsTagGroupExpanded ? tagsList.RowHeight * vm.VisibleTags.Count : 0;
            var desiredSubmodule = vm.IsSubmoduleGroupExpanded ? submoduleList.RowHeight * vm.Submodules.Count : 0;
            var desiredWorktree = vm.IsWorktreeGroupExpanded ? worktreeList.RowHeight * vm.Worktrees.Count : 0;
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
                tagsList.Height = height;
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
                submoduleList.Height = height;
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
                worktreeList.Height = height;
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
                            localBranchTree.Height = local;
                            remoteBranchTree.Height = leftHeight - local;
                        }
                        else if (remote < half)
                        {
                            remoteBranchTree.Height = remote;
                            localBranchTree.Height = leftHeight - remote;
                        }
                        else
                        {
                            localBranchTree.Height = half;
                            remoteBranchTree.Height = half;
                        }
                    }
                    else
                    {
                        localBranchTree.Height = leftHeight;
                    }
                }
                else if (vm.IsRemoteGroupExpanded)
                {
                    remoteBranchTree.Height = leftHeight;
                }
            }
            else
            {
                if (vm.IsLocalBranchGroupExpanded)
                {
                    var height = localBranchRows * 24;
                    localBranchTree.Height = height;
                }

                if (vm.IsRemoteGroupExpanded)
                {
                    var height = remoteBranchRows * 24;
                    remoteBranchTree.Height = height;
                }
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
