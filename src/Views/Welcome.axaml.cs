using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class Welcome : UserControl
    {
        public Welcome()
        {
            InitializeComponent();
        }

        private void SetupTreeViewDragAndDrop(object sender, RoutedEventArgs e)
        {
            if (sender is TreeView view)
            {
                DragDrop.SetAllowDrop(view, true);
                view.AddHandler(DragDrop.DragOverEvent, DragOverTreeView);
                view.AddHandler(DragDrop.DropEvent, DropOnTreeView);
            }
        }

        private void SetupTreeNodeDragAndDrop(object sender, RoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                DragDrop.SetAllowDrop(grid, true);
                grid.AddHandler(DragDrop.DragOverEvent, DragOverTreeNode);
                grid.AddHandler(DragDrop.DropEvent, DropOnTreeNode);
            }
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Grid grid && DataContext is ViewModels.Welcome vm)
            {
                var menu = vm.CreateContextMenu(grid.DataContext as ViewModels.RepositoryNode);
                menu?.Open(grid);
                e.Handled = true;
            }
        }

        private void OnPointerPressedTreeNode(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
            {
                _pressedTreeNode = true;
                _startDragTreeNode = false;
                _pressedTreeNodePosition = e.GetPosition(sender as Grid);
            }
            else
            {
                _pressedTreeNode = false;
                _startDragTreeNode = false;
            }
        }

        private void OnPointerReleasedOnTreeNode(object sender, PointerReleasedEventArgs e)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnPointerMovedOverTreeNode(object sender, PointerEventArgs e)
        {
            if (_pressedTreeNode && !_startDragTreeNode && sender is Grid grid)
            {
                var delta = e.GetPosition(grid) - _pressedTreeNodePosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTreeNode = true;

                var data = new DataObject();
                data.Set("MovedRepositoryTreeNode", grid.DataContext);
                DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            }
        }

        private void OnTreeViewLostFocus(object sender, RoutedEventArgs e)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeView(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") || e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private async void DropOnTreeView(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode"))
            {
                e.Handled = true;

                var moved = e.Data.Get("MovedRepositoryTreeNode") as ViewModels.RepositoryNode;
                if (moved != null && DataContext is ViewModels.Welcome vm)
                {
                    vm.MoveNode(moved, null);
                }
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                foreach (var item in items)
                {
                    await OpenOrInitRepository(item.Path.LocalPath);
                    break;
                }
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeNode(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") || e.Data.Contains(DataFormats.Files))
            {
                var grid = sender as Grid;
                if (grid == null)
                    return;

                var to = grid.DataContext as ViewModels.RepositoryNode;
                if (to == null)
                    return;

                if (to.IsRepository)
                {
                    e.DragEffects = DragDropEffects.None;
                    e.Handled = true;
                }
                else
                {
                    e.DragEffects = DragDropEffects.Move;
                    e.Handled = true;
                }
            }
        }

        private async void DropOnTreeNode(object sender, DragEventArgs e)
        {
            var grid = sender as Grid;
            if (grid == null)
                return;

            var to = grid.DataContext as ViewModels.RepositoryNode;
            if (to == null || to.IsRepository)
            {
                e.Handled = true;
                return;
            }

            if (e.Data.Contains("MovedRepositoryTreeNode"))
            {
                e.Handled = true;

                var moved = e.Data.Get("MovedRepositoryTreeNode") as ViewModels.RepositoryNode;
                if (to != null && moved != null && to != moved && DataContext is ViewModels.Welcome vm)
                {
                    vm.MoveNode(moved, to);
                }
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                foreach (var item in items)
                {
                    await OpenOrInitRepository(item.Path.LocalPath, to);
                    break;
                }
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnDoubleTappedTreeNode(object sender, TappedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid == null)
                return;

            var to = grid.DataContext as ViewModels.RepositoryNode;
            if (to == null || !to.IsRepository)
            {
                return;
            }

            var launcher = TopLevel.GetTopLevel(this).DataContext as ViewModels.Launcher;
            launcher.OpenRepositoryInTab(to, launcher.ActivePage);
            e.Handled = true;
        }

        private async void OpenLocalRepository(object sender, RoutedEventArgs e)
        {
            if (!ViewModels.PopupHost.CanCreatePopup())
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
            {
                await OpenOrInitRepository(selected[0].Path.LocalPath);
            }
        }

        private Task OpenOrInitRepository(string path, ViewModels.RepositoryNode parent = null)
        {
            var launcher = TopLevel.GetTopLevel(this).DataContext as ViewModels.Launcher;
            var page = launcher.ActivePage;

            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path);
                else
                    return null;
            }

            return Task.Run(() =>
            {
                var root = new Commands.QueryRepositoryRootPath(path).Result();
                if (string.IsNullOrEmpty(root))
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        (DataContext as ViewModels.Welcome).InitRepository(path);
                    });
                    return;
                }

                var gitDir = new Commands.QueryGitDir(root).Result();
                Dispatcher.UIThread.Invoke(() =>
                {
                    var repo = ViewModels.Preference.AddRepository(root, gitDir);
                    var node = new ViewModels.RepositoryNode()
                    {
                        Id = repo.FullPath,
                        Name = Path.GetFileName(repo.FullPath),
                        Bookmark = 0,
                        IsRepository = true,
                    };
                    ViewModels.Preference.AddNode(node, parent);
                    launcher.OpenRepositoryInTab(node, page);
                });
            });
        }

        private bool _pressedTreeNode = false;
        private Point _pressedTreeNodePosition = new Point();
        private bool _startDragTreeNode = false;
    }
}
