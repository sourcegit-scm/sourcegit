using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Welcome : UserControl
    {
        public Welcome()
        {
            InitializeComponent();
        }

        private void SetupTreeViewDragAndDrop(object sender, RoutedEventArgs _)
        {
            if (sender is TreeView view)
            {
                DragDrop.SetAllowDrop(view, true);
                view.AddHandler(DragDrop.DragOverEvent, DragOverTreeView);
                view.AddHandler(DragDrop.DropEvent, DropOnTreeView);
            }
        }

        private void SetupTreeNodeDragAndDrop(object sender, RoutedEventArgs _)
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
                grid.OpenContextMenu(menu);
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

        private void OnPointerReleasedOnTreeNode(object _1, PointerReleasedEventArgs _2)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnPointerMovedOverTreeNode(object sender, PointerEventArgs e)
        {
            if (_pressedTreeNode && !_startDragTreeNode &&
                sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var delta = e.GetPosition(grid) - _pressedTreeNodePosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTreeNode = true;

                var data = new DataObject();
                data.Set("MovedRepositoryTreeNode", node);
                DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            }
        }

        private void OnTreeViewLostFocus(object _1, RoutedEventArgs _2)
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

        private void DropOnTreeView(object sender, DragEventArgs e)
        {
            if (e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;

                if (DataContext is ViewModels.Welcome vm)
                    vm.MoveNode(moved, null);
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        OpenOrInitRepository(item.Path.LocalPath);
                        break;
                    }
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

        private void DropOnTreeNode(object sender, DragEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            var to = grid.DataContext as ViewModels.RepositoryNode;
            if (to == null || to.IsRepository)
            {
                e.Handled = true;
                return;
            }

            if (e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;

                if (to != moved && DataContext is ViewModels.Welcome vm)
                    vm.MoveNode(moved, to);
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        OpenOrInitRepository(item.Path.LocalPath, to);
                        break;
                    }
                }
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnDoubleTappedTreeNode(object sender, TappedEventArgs e)
        {
            var grid = sender as Grid;
            var to = grid?.DataContext as ViewModels.RepositoryNode;
            if (to is not { IsRepository: true })
                return;

            var parent = this.FindAncestorOfType<Launcher>();
            if (parent?.DataContext is ViewModels.Launcher launcher)
                launcher.OpenRepositoryInTab(to, null);

            e.Handled = true;
        }

        private void OpenOrInitRepository(string path, ViewModels.RepositoryNode parent = null)
        {
            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path);
                else
                    return;
            }

            var root = new Commands.QueryRepositoryRootPath(path).Result();
            if (string.IsNullOrEmpty(root))
            {
                (DataContext as ViewModels.Welcome)?.InitRepository(path, parent);
                return;
            }

            var normalizedPath = root.Replace("\\", "/");
            var node = ViewModels.Preference.FindOrAddNodeByRepositoryPath(normalizedPath, parent, true);
            var launcher = this.FindAncestorOfType<Launcher>()?.DataContext as ViewModels.Launcher;
            launcher?.OpenRepositoryInTab(node, launcher.ActivePage);
        }

        private bool _pressedTreeNode = false;
        private Point _pressedTreeNodePosition = new Point();
        private bool _startDragTreeNode = false;
    }
}
