using System.Collections.Generic;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class WorkingCopy : UserControl
    {
        public WorkingCopy()
        {
            InitializeComponent();
        }

        private void ViewAssumeUnchanged(object sender, RoutedEventArgs e)
        {
            var repoPage = this.FindAncestorOfType<Repository>();
            if (repoPage != null)
            {
                var repo = (repoPage.DataContext as ViewModels.Repository).FullPath;
                var window = new AssumeUnchangedManager();
                window.DataContext = new ViewModels.AssumeUnchangedManager(repo);
                window.ShowDialog((Window)TopLevel.GetTopLevel(this));
            }

            e.Handled = true;
        }

        private void StageSelected(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.WorkingCopy;
            if (vm == null)
                return;

            List<Models.Change> selected = new List<Models.Change>();
            switch (ViewModels.Preference.Instance.UnstagedChangeViewMode)
            {
                case Models.ChangeViewMode.List:
                    foreach (var item in unstagedList.SelectedItems)
                    {
                        if (item is Models.Change change)
                            selected.Add(change);
                    }
                    break;
                case Models.ChangeViewMode.Grid:
                    foreach (var item in unstagedGrid.SelectedItems)
                    {
                        if (item is Models.Change change)
                            selected.Add(change);
                    }
                    break;
                default:
                    foreach (var item in unstagedTree.SelectedItems)
                    {
                        if (item is ViewModels.FileTreeNode node)
                            CollectChangesFromNode(selected, node);
                    }
                    break;
            }

            vm.StageChanges(selected);
            e.Handled = true;
        }

        private void StageAll(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.WorkingCopy;
            if (vm == null)
                return;

            vm.StageChanges(vm.Unstaged);
            e.Handled = true;
        }

        private void UnstageSelected(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.WorkingCopy;
            if (vm == null)
                return;

            List<Models.Change> selected = new List<Models.Change>();
            switch (ViewModels.Preference.Instance.StagedChangeViewMode)
            {
                case Models.ChangeViewMode.List:
                    foreach (var item in stagedList.SelectedItems)
                    {
                        if (item is Models.Change change)
                            selected.Add(change);
                    }
                    break;
                case Models.ChangeViewMode.Grid:
                    foreach (var item in stagedGrid.SelectedItems)
                    {
                        if (item is Models.Change change)
                            selected.Add(change);
                    }
                    break;
                default:
                    foreach (var item in stagedTree.SelectedItems)
                    {
                        if (item is ViewModels.FileTreeNode node)
                            CollectChangesFromNode(selected, node);
                    }
                    break;
            }

            vm.UnstageChanges(selected);
            e.Handled = true;
        }

        private void UnstageAll(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.WorkingCopy;
            if (vm == null)
                return;

            vm.UnstageChanges(vm.Staged);
            e.Handled = true;
        }

        private void OnUnstagedListKeyDown(object sender, KeyEventArgs e)
        {
            var datagrid = sender as DataGrid;
            if (datagrid.SelectedItems.Count > 0 && e.Key == Key.Space && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in datagrid.SelectedItems)
                {
                    if (item is Models.Change change)
                        selected.Add(change);
                }

                vm.StageChanges(selected);
            }

            e.Handled = true;
        }

        private void OnUnstagedTreeViewKeyDown(object sender, KeyEventArgs e)
        {
            var tree = sender as TreeView;
            if (tree.SelectedItems.Count > 0 && e.Key == Key.Space && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in tree.SelectedItems)
                {
                    if (item is ViewModels.FileTreeNode node)
                        CollectChangesFromNode(selected, node);
                }

                vm.StageChanges(selected);
            }

            e.Handled = true;
        }

        private void OnStagedListKeyDown(object sender, KeyEventArgs e)
        {
            var datagrid = sender as DataGrid;
            if (datagrid.SelectedItems.Count > 0 && e.Key == Key.Space && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in datagrid.SelectedItems)
                {
                    if (item is Models.Change change)
                        selected.Add(change);
                }

                vm.UnstageChanges(selected);
            }

            e.Handled = true;
        }

        private void OnStagedTreeViewKeyDown(object sender, KeyEventArgs e)
        {
            var tree = sender as TreeView;
            if (tree.SelectedItems.Count > 0 && e.Key == Key.Space && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in tree.SelectedItems)
                {
                    if (item is ViewModels.FileTreeNode node)
                        CollectChangesFromNode(selected, node);
                }

                vm.UnstageChanges(selected);
            }

            e.Handled = true;
        }

        private void OnUnstagedListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var datagrid = sender as DataGrid;
            if (datagrid.SelectedItems.Count > 0 && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in datagrid.SelectedItems)
                {
                    if (item is Models.Change change)
                        selected.Add(change);
                }

                var menu = vm.CreateContextMenuForUnstagedChanges(selected);
                if (menu != null)
                    menu.Open(datagrid);
            }

            e.Handled = true;
        }

        private void OnUnstagedTreeViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var tree = sender as TreeView;
            if (tree.SelectedItems.Count > 0 && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in tree.SelectedItems)
                {
                    if (item is ViewModels.FileTreeNode node)
                        CollectChangesFromNode(selected, node);
                }

                var menu = vm.CreateContextMenuForUnstagedChanges(selected);
                if (menu != null)
                    menu.Open(tree);
            }

            e.Handled = true;
        }

        private void OnStagedListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var datagrid = sender as DataGrid;
            if (datagrid.SelectedItems.Count > 0 && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in datagrid.SelectedItems)
                {
                    if (item is Models.Change change)
                        selected.Add(change);
                }

                var menu = vm.CreateContextMenuForStagedChanges(selected);
                if (menu != null)
                    menu.Open(datagrid);
            }

            e.Handled = true;
        }

        private void OnStagedTreeViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var tree = sender as TreeView;
            if (tree.SelectedItems.Count > 0 && DataContext is ViewModels.WorkingCopy vm)
            {
                List<Models.Change> selected = new List<Models.Change>();
                foreach (var item in tree.SelectedItems)
                {
                    if (item is ViewModels.FileTreeNode node)
                        CollectChangesFromNode(selected, node);
                }

                var menu = vm.CreateContextMenuForStagedChanges(selected);
                if (menu != null)
                    menu.Open(tree);
            }

            e.Handled = true;
        }

        private void StartAmend(object sender, RoutedEventArgs e)
        {
            var repoPage = this.FindAncestorOfType<Repository>();
            if (repoPage != null)
            {
                var repo = (repoPage.DataContext as ViewModels.Repository).FullPath;
                var commits = new Commands.QueryCommits(repo, "-n 1", false).Result();
                if (commits.Count == 0)
                {
                    App.RaiseException(repo, "No commits to amend!!!");

                    var chkBox = sender as CheckBox;
                    chkBox.IsChecked = false;
                    e.Handled = true;
                    return;
                }

                var vm = DataContext as ViewModels.WorkingCopy;
                vm.CommitMessage = commits[0].FullMessage;
            }

            e.Handled = true;
        }

        private void Commit(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.WorkingCopy;
            vm.DoCommit(false);
            e.Handled = true;
        }

        private void CommitWithPush(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.WorkingCopy;
            vm.DoCommit(true);
            e.Handled = true;
        }

        private void CollectChangesFromNode(List<Models.Change> outs, ViewModels.FileTreeNode node)
        {
            if (node.IsFolder)
            {
                foreach (var child in node.Children)
                    CollectChangesFromNode(outs, child);
            }
            else
            {
                var change = node.Backend as Models.Change;
                if (change != null && !outs.Contains(change))
                    outs.Add(change);
            }
        }

        private void OnOpenCommitMessagePicker(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.WorkingCopy vm)
            {
                var menu = vm.CreateContextMenuForCommitMessages();
                menu.Placement = PlacementMode.TopEdgeAlignedLeft;
                menu.Open(button);
                e.Handled = true;
            }
        }
    }
}
