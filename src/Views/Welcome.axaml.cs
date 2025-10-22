using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class RepositoryTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.RepositoryNode { IsRepository: false } node)
                ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);

            e.Handled = true;
        }
    }

    public class RepositoryListBox : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (SelectedItem is ViewModels.RepositoryNode node && e.KeyModifiers == KeyModifiers.None)
            {
                if (e.Key is Key.Delete or Key.Back)
                {
                    node.Delete();
                    e.Handled = true;
                }
                else if (node.IsRepository)
                {
                    if (e.Key == Key.Enter)
                    {
                        node.Open();
                        e.Handled = true;
                    }
                }
                else if ((node.IsExpanded && e.Key == Key.Left) || (!node.IsExpanded && e.Key == Key.Right) || e.Key == Key.Enter)
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

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            await ViewModels.Welcome.Instance.UpdateStatusAsync(false, _cancellation.Token);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _cancellation.Cancel();
            base.OnUnloaded(e);
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

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs ev)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var menu = new ContextMenu();

                if (!node.IsRepository && node.SubNodes.Count > 0)
                {
                    var openAll = new MenuItem();
                    openAll.Header = App.Text("Welcome.OpenAllInNode");
                    openAll.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                    openAll.Click += (_, e) =>
                    {
                        node.Open();
                        e.Handled = true;
                    };

                    menu.Items.Add(openAll);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                if (node.IsRepository)
                {
                    var open = new MenuItem();
                    open.Header = App.Text("Welcome.OpenOrInit");
                    open.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                    open.Click += (_, e) =>
                    {
                        node.Open();
                        e.Handled = true;
                    };

                    var explore = new MenuItem();
                    explore.Header = App.Text("Repository.Explore");
                    explore.Icon = App.CreateMenuIcon("Icons.Explore");
                    explore.Click += (_, e) =>
                    {
                        node.OpenInFileManager();
                        e.Handled = true;
                    };

                    var terminal = new MenuItem();
                    terminal.Header = App.Text("Repository.Terminal");
                    terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
                    terminal.Click += (_, e) =>
                    {
                        node.OpenTerminal();
                        e.Handled = true;
                    };

                    menu.Items.Add(open);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(explore);
                    menu.Items.Add(terminal);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
                else
                {
                    var addSubFolder = new MenuItem();
                    addSubFolder.Header = App.Text("Welcome.AddSubFolder");
                    addSubFolder.Icon = App.CreateMenuIcon("Icons.Folder.Add");
                    addSubFolder.Click += (_, e) =>
                    {
                        node.AddSubFolder();
                        e.Handled = true;
                    };
                    menu.Items.Add(addSubFolder);
                }

                var edit = new MenuItem();
                edit.Header = App.Text("Welcome.Edit");
                edit.Icon = App.CreateMenuIcon("Icons.Edit");
                edit.Click += (_, e) =>
                {
                    node.Edit();
                    e.Handled = true;
                };

                var move = new MenuItem();
                move.Header = App.Text("Welcome.Move");
                move.Icon = App.CreateMenuIcon("Icons.MoveTo");
                move.Click += (_, e) =>
                {
                    node.Move();
                    e.Handled = true;
                };

                var delete = new MenuItem();
                delete.Header = App.Text("Welcome.Delete");
                delete.Icon = App.CreateMenuIcon("Icons.Clear");
                delete.Click += (_, e) =>
                {
                    node.Delete();
                    e.Handled = true;
                };

                menu.Items.Add(edit);
                menu.Items.Add(move);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(delete);
                menu.Open(grid);
            }

            ev.Handled = true;
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

        private async void OnPointerMovedOverTreeNode(object sender, PointerEventArgs e)
        {
            if (_pressedTreeNode && !_startDragTreeNode &&
                sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var delta = e.GetPosition(grid) - _pressedTreeNodePosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTreeNode = true;

                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(_dndRepoNode, node.Id));
                await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
            }
        }

        private void OnTreeViewLostFocus(object _1, RoutedEventArgs _2)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeView(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(_dndRepoNode))
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
            if (e.DataTransfer.TryGetValue(_dndRepoNode) is { Length: > 1 } nodeId)
            {
                var moved = ViewModels.Welcome.Instance.FindNodeById(nodeId);
                ViewModels.Welcome.Instance.MoveNode(moved, null);
                e.Handled = true;
            }
            else if (e.DataTransfer.Contains(DataFormat.File))
            {
                e.Handled = true;

                var items = e.DataTransfer.TryGetFiles() ?? [];
                var refresh = false;

                foreach (var item in items)
                {
                    var path = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(item.Path.LocalPath);
                    if (!string.IsNullOrEmpty(path))
                    {
                        await ViewModels.Welcome.Instance.AddRepositoryAsync(path, null, true, false);
                        refresh = true;
                    }
                }

                if (refresh)
                    ViewModels.Welcome.Instance.Refresh();
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeNode(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(_dndRepoNode))
            {
                if (sender is not Grid { DataContext: ViewModels.RepositoryNode })
                    return;

                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private async void DropOnTreeNode(object sender, DragEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            if (grid.DataContext is not ViewModels.RepositoryNode to)
            {
                e.Handled = true;
                return;
            }

            if (to.IsRepository)
                to = ViewModels.Welcome.Instance.FindParentGroup(to);

            if (e.DataTransfer.TryGetValue(_dndRepoNode) is { } nodeId)
            {
                e.Handled = true;

                var moved = ViewModels.Welcome.Instance.FindNodeById(nodeId);
                if (to != moved)
                    ViewModels.Welcome.Instance.MoveNode(moved, to);
            }
            else if (e.DataTransfer.Contains(DataFormat.File))
            {
                e.Handled = true;

                var items = e.DataTransfer.TryGetFiles() ?? [];
                var refresh = false;

                foreach (var item in items)
                {
                    var path = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(item.Path.LocalPath);
                    if (!string.IsNullOrEmpty(path))
                    {
                        await ViewModels.Welcome.Instance.AddRepositoryAsync(path, to, true, false);
                        refresh = true;
                    }
                }

                if (refresh)
                    ViewModels.Welcome.Instance.Refresh();
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnDoubleTappedTreeNode(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node })
            {
                if (node.IsRepository)
                    node.Open();
                else
                    ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);

                e.Handled = true;
            }
        }

        private bool _pressedTreeNode = false;
        private Point _pressedTreeNodePosition = new Point();
        private bool _startDragTreeNode = false;
        private readonly DataFormat<string> _dndRepoNode = DataFormat.CreateStringApplicationFormat("sourcegit-dnd-repo-node");
        private CancellationTokenSource _cancellation = new CancellationTokenSource();
    }
}
