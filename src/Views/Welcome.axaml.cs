using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class RepositoryTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.RepositoryNode { IsRepository: false } node)
            {
                ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class RepositoryListBox : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (SelectedItem is ViewModels.RepositoryNode { IsRepository: false } node && e.KeyModifiers == KeyModifiers.None)
            {
                if ((node.IsExpanded && e.Key == Key.Left) || (!node.IsExpanded && e.Key == Key.Right))
                {
                    ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
                    e.Handled = true;
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }
    }

    public partial class Welcome : UserControl
    {
        public Welcome()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled)
            {
                if (e.Key == Key.Down && ViewModels.Welcome.Instance.Rows.Count > 0)
                {
                    TreeContainer.SelectedIndex = 0;
                    TreeContainer.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    ViewModels.Welcome.Instance.ClearSearchFilter();
                    e.Handled = true;
                }
            }
        }

        private void SetupTreeViewDragAndDrop(object sender, RoutedEventArgs _)
        {
            if (sender is ListBox view)
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

        private void OnTreeViewKeyDown(object _, KeyEventArgs e)
        {
            if (TreeContainer.SelectedItem is ViewModels.RepositoryNode node && e.Key == Key.Enter)
            {
                if (node.IsRepository)
                {
                    var parent = this.FindAncestorOfType<Launcher>();
                    if (parent is { DataContext: ViewModels.Launcher launcher })
                        launcher.OpenRepositoryInTab(node, null);
                }
                else
                {
                    ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
                }

                e.Handled = true;
            }
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var menu = ViewModels.Welcome.Instance.CreateContextMenu(node);
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
            if (e.Data.Contains("MovedRepositoryTreeNode") && e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;
                ViewModels.Welcome.Instance.MoveNode(moved, null);
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

            if (e.Data.Contains("MovedRepositoryTreeNode") &&
                e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;

                if (to != moved)
                    ViewModels.Welcome.Instance.MoveNode(moved, to);
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
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node })
            {
                if (node.IsRepository)
                {
                    var parent = this.FindAncestorOfType<Launcher>();
                    if (parent is { DataContext: ViewModels.Launcher launcher })
                        launcher.OpenRepositoryInTab(node, null);
                }
                else
                {
                    ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
                }

                e.Handled = true;
            }
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

            var isBare = new Commands.IsBareRepository(path).Result();
            if (isBare)
            {
                App.RaiseException(string.Empty, $"'{path}' is a bare repository, which is not supported by SourceGit!");
                return;
            }

            var test = new Commands.QueryRepositoryRootPath(path).ReadToEnd();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                ViewModels.Welcome.Instance.InitRepository(path, parent, test.StdErr);
                return;
            }

            var node = ViewModels.Preferences.Instance.FindOrAddNodeByRepositoryPath(test.StdOut.Trim(), parent, true);
            ViewModels.Welcome.Instance.Refresh();

            var launcher = this.FindAncestorOfType<Launcher>()?.DataContext as ViewModels.Launcher;
            launcher?.OpenRepositoryInTab(node, launcher.ActivePage);
        }

        private bool _pressedTreeNode = false;
        private Point _pressedTreeNodePosition = new Point();
        private bool _startDragTreeNode = false;
    }
}
