using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

using SourceGit.ViewModels;

namespace SourceGit.Views
{
    public class RepositorySubView : Border
    {
        public static readonly StyledProperty<object> DataProperty =
            AvaloniaProperty.Register<RepositorySubView, object>(nameof(Data), false);

        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Border);

        static RepositorySubView()
        {
            DataProperty.Changed.AddClassHandler<RepositorySubView>((view, ev) =>
            {
                var data = view.Data;

                if (data == null)
                {
                    view.Child = null;
                }
                else if (data is ViewModels.Histories)
                {
                    view.Child = new Histories { DataContext = data };
                }
                else if (data is ViewModels.WorkingCopy)
                {
                    view.Child = new WorkingCopy { DataContext = data };
                }
                else if (data is ViewModels.StashesPage)
                {
                    view.Child = new StashesPage { DataContext = data };
                }
                else
                {
                    view.Child = null;
                }

                GC.Collect();
            });
        }
    }

    public partial class Repository : UserControl
    {
        public Repository()
        {
            InitializeComponent();
        }

        private void OnLocalBranchTreeLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TreeView tree) tree.UnselectAll();
        }

        private void OnRemoteBranchTreeLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TreeView tree) tree.UnselectAll();
        }

        private void OnTagDataGridLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid datagrid) datagrid.SelectedItem = null;
        }

        private void OnLocalBranchTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TreeView tree && tree.SelectedItem != null)
            {
                remoteBranchTree.UnselectAll();

                var node = tree.SelectedItem as Models.BranchTreeNode;
                if (node.IsBranch && DataContext is ViewModels.Repository repo)
                {
                    repo.NavigateToCommit((node.Backend as Models.Branch).Head);
                }
            }
        }

        private void OnRemoteBranchTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TreeView tree && tree.SelectedItem != null)
            {
                localBranchTree.UnselectAll();

                var node = tree.SelectedItem as Models.BranchTreeNode;
                if (node.IsBranch && DataContext is ViewModels.Repository repo)
                {
                    repo.NavigateToCommit((node.Backend as Models.Branch).Head);
                }
            }
        }

        private void OnTagDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null)
            {
                var tag = datagrid.SelectedItem as Models.Tag;
                if (DataContext is ViewModels.Repository repo)
                {
                    repo.NavigateToCommit(tag.SHA);
                }
            }
        }

        private void OnSearchCommitPanelPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            var grid = sender as Grid;
            if (e.Property == IsVisibleProperty && grid.IsVisible)
            {
                txtSearchCommitsBox.Focus();
            }
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.Repository repo)
                {
                    repo.StartSearchCommits();
                }
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

        private void OnToggleFilter(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                var filter = string.Empty;
                if (toggle.DataContext is Models.BranchTreeNode node)
                {
                    if (node.IsBranch)
                    {
                        filter = (node.Backend as Models.Branch).FullName;
                    }
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

        private void OnLocalBranchContextMenuRequested(object sender, ContextRequestedEventArgs e)
        {
            remoteBranchTree.UnselectAll();

            if (sender is Grid grid && grid.DataContext is Models.BranchTreeNode node)
            {
                if (node.IsBranch && DataContext is ViewModels.Repository repo)
                {
                    var menu = repo.CreateContextMenuForLocalBranch(node.Backend as Models.Branch);
                    if (menu != null) menu.Open(grid);
                }
            }

            e.Handled = true;
        }

        private void OnRemoteBranchContextMenuRequested(object sender, ContextRequestedEventArgs e)
        {
            localBranchTree.UnselectAll();

            if (sender is Grid grid && grid.DataContext is Models.BranchTreeNode node && DataContext is ViewModels.Repository repo)
            {
                if (node.IsRemote)
                {
                    var menu = repo.CreateContextMenuForRemote(node.Backend as Models.Remote);
                    if (menu != null) menu.Open(grid);
                }
                else if (node.IsBranch)
                {
                    var menu = repo.CreateContextMenuForRemoteBranch(node.Backend as Models.Branch);
                    if (menu != null) menu.Open(grid);
                }
            }

            e.Handled = true;
        }

        private void OnTagContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var tag = datagrid.SelectedItem as Models.Tag;
                var menu = repo.CreateContextMenuForTag(tag);
                if (menu != null) menu.Open(datagrid);
            }

            e.Handled = true;
        }

        private void OnSubmoduleContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid datagrid && datagrid.SelectedItem != null && DataContext is ViewModels.Repository repo)
            {
                var submodule = datagrid.SelectedItem as string;
                var menu = repo.CreateContextMenuForSubmodule(submodule);
                if (menu != null) menu.Open(datagrid);
            }

            e.Handled = true;
        }

        private void OpenGitFlowMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForGitFlow();
                if (menu != null) menu.Open(sender as Button);
            }

            e.Handled = true;
        }

        private async void UpdateSubmodules(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                repo.SetWatcherEnabled(false);
                iconSubmoduleUpdate.Classes.Add("rotating");
                await Task.Run(() => new Commands.Submodule(repo.FullPath).Update());
                iconSubmoduleUpdate.Classes.Remove("rotating");
                repo.SetWatcherEnabled(true);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedLocalBranchNode(object sender, TappedEventArgs e)
        {
            if (!PopupHost.CanCreatePopup()) return;

            if (sender is Grid grid && DataContext is ViewModels.Repository repo)
            {
                var node = grid.DataContext as Models.BranchTreeNode;
                if (node != null && node.IsBranch)
                {
                    var branch = node.Backend as Models.Branch;
                    if (branch.IsCurrent) return;

                    PopupHost.ShowAndStartPopup(new ViewModels.Checkout(repo, branch.Name));
                    e.Handled = true;
                }
            }
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
    }
}