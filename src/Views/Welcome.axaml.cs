using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

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
                ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);

            e.Handled = true;
        }
    }

    public class RepositoryListBox : ListBoxEx
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (SelectedItems is { Count: 1 } && SelectedItem is ViewModels.RepositoryNode node && e.KeyModifiers == KeyModifiers.None)
            {
                if (e.Key is Key.Left)
                {
                    if (node.IsExpanded)
                        ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
                    else if (FindParent(node) is { } parent)
                        Select(parent);

                    e.Handled = true;
                }
                else if (e.Key is Key.Right && node.SubNodes.Count > 0)
                {
                    if (!node.IsExpanded)
                        ViewModels.Welcome.Instance.ToggleNodeIsExpanded(node);
                    else
                        Select(node.SubNodes[0]);
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }

        private ViewModels.RepositoryNode FindParent(ViewModels.RepositoryNode item)
        {
            if (item.Depth == 0)
                return null;

            var idx = Items.IndexOf(item);
            if (idx < 1)
                return null;

            for (var i = idx - 1; i >= 0; i--)
            {
                if (Items[i] is ViewModels.RepositoryNode node && node.Depth < item.Depth)
                    return node;
            }

            return null;
        }
    }

    public partial class Welcome : UserControl
    {
        public Welcome()
        {
            InitializeComponent();

            TreeContainer.AddHandler(PointerMovedEvent, OnPointerMovedOverTree, RoutingStrategies.Tunnel, true);
            TreeContainer.AddHandler(PointerReleasedEvent, OnPointerReleasedOnTree, RoutingStrategies.Tunnel, true);

            // Rubber-band selection can start on any empty space of the page.
            AddHandler(PointerPressedEvent, OnPagePointerPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, OnPagePointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            AddHandler(PointerReleasedEvent, OnPagePointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            AddHandler(PointerCaptureLostEvent, (_, _) => EndRubberBand(), RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            SearchBox.Focus(NavigationMethod.Directional);
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

            if (!e.Handled && e.KeyModifiers == KeyModifiers.None)
            {
                if (e.Key == Key.Down && ViewModels.Welcome.Instance.Rows.Count > 0)
                {
                    TreeContainer.SelectedIndex = 0;
                    TreeContainer.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    var page = this.FindAncestorOfType<LauncherPage>();
                    if (page is { DataContext: ViewModels.LauncherPage ctx } && ctx.Popup == null)
                        OnClearSearchFilter(this, e);
                }
                else if (e.Key == Key.Enter)
                {
                    if (TreeContainer.SelectedItems is { Count: 1 } &&
                        TreeContainer.SelectedItem is ViewModels.RepositoryNode { IsRepository: true } node)
                    {
                        node.Open();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Delete || e.Key == Key.Back)
                {
                    if (TreeContainer.SelectedItems is { Count: 1 } &&
                        TreeContainer.SelectedItem is ViewModels.RepositoryNode node)
                    {
                        node.Delete();
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnSearchHotKey(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }

        private void OnClearSearchFilter(object sender, RoutedEventArgs e)
        {
            ViewModels.Welcome.Instance.ClearSearchFilter();
            SearchBox.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs ev)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var menu = new ContextMenu();

                var selected = GetSelectedNodes();
                if (selected.Count > 1 && selected.Contains(node))
                {
                    var moveSelected = new MenuItem();
                    moveSelected.Header = App.Text("Welcome.MoveSelected", selected.Count);
                    moveSelected.Icon = this.CreateMenuIcon("Icons.MoveTo");
                    moveSelected.Click += (_, e) =>
                    {
                        var activePage = App.GetLauncher().ActivePage;
                        if (activePage != null && activePage.CanCreatePopup())
                            activePage.Popup = new ViewModels.MoveRepositoryNode(selected);
                        e.Handled = true;
                    };

                    menu.Items.Add(moveSelected);
                    menu.Open(grid);
                    ev.Handled = true;
                    return;
                }

                if (!selected.Contains(node) || selected.Count != 1)
                    TreeContainer.SelectedItem = node;

                var edit = new MenuItem();
                edit.Header = App.Text("Welcome.Edit");
                edit.Icon = this.CreateMenuIcon("Icons.Edit");
                edit.Click += (_, e) =>
                {
                    node.Edit();
                    e.Handled = true;
                };

                var move = new MenuItem();
                move.Header = App.Text("Welcome.Move");
                move.Icon = this.CreateMenuIcon("Icons.MoveTo");
                move.Click += (_, e) =>
                {
                    node.Move();
                    e.Handled = true;
                };

                var delete = new MenuItem();
                delete.Header = App.Text("Welcome.Delete");
                delete.Icon = this.CreateMenuIcon("Icons.Clear");
                delete.Click += (_, e) =>
                {
                    node.Delete();
                    e.Handled = true;
                };

                if (!node.IsRepository)
                {
                    if (node.SubNodes.Count > 0)
                    {
                        var openAll = new MenuItem();
                        openAll.Header = App.Text("Welcome.OpenAllInNode");
                        openAll.Icon = this.CreateMenuIcon("Icons.Folder.Open");
                        openAll.Click += (_, e) =>
                        {
                            node.Open();
                            e.Handled = true;
                        };

                        menu.Items.Add(openAll);
                        menu.Items.Add(new MenuItem() { Header = "-" });
                    }

                    var addSubFolder = new MenuItem();
                    addSubFolder.Header = App.Text("Welcome.AddSubFolder");
                    addSubFolder.Icon = this.CreateMenuIcon("Icons.Folder.Add");
                    addSubFolder.Click += (_, e) =>
                    {
                        node.AddSubFolder();
                        e.Handled = true;
                    };

                    menu.Items.Add(addSubFolder);
                    menu.Items.Add(edit);
                    menu.Items.Add(move);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(delete);
                }
                else if (Directory.Exists(node.Id))
                {
                    var open = new MenuItem();
                    open.Header = App.Text("Welcome.OpenOrInit");
                    open.Icon = this.CreateMenuIcon("Icons.Folder.Open");
                    open.Click += (_, e) =>
                    {
                        node.Open();
                        e.Handled = true;
                    };

                    var explore = new MenuItem();
                    explore.Header = App.Text("Repository.Explore");
                    explore.Icon = this.CreateMenuIcon("Icons.Explore");
                    explore.Click += (_, e) =>
                    {
                        node.OpenInFileManager();
                        e.Handled = true;
                    };

                    var terminal = new MenuItem();
                    terminal.Header = App.Text("Repository.Terminal");
                    terminal.Icon = this.CreateMenuIcon("Icons.Terminal");
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
                    menu.Items.Add(edit);
                    menu.Items.Add(move);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(delete);
                }
                else
                {
                    menu.Items.Add(delete);
                }

                menu.Open(grid);
            }

            ev.Handled = true;
        }

        private void OnPointerPressedTreeNode(object sender, PointerPressedEventArgs e)
        {
            _pendingSingleSelect = null;

            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed &&
                sender is Grid { DataContext: ViewModels.RepositoryNode pressed })
            {
                _pressTreeNodeEvent = e;
                _pressedTreeNode = pressed;
                _startDragTreeNode = false;

                // Pressing an item that is part of a multi-selection must keep the selection, so that the whole
                // selection can be dragged. The single selection is applied on release if no drag happened.
                if (e.KeyModifiers == KeyModifiers.None &&
                    sender is Grid { DataContext: ViewModels.RepositoryNode node } &&
                    TreeContainer.SelectedItems is { Count: > 1 } selected &&
                    selected.Contains(node))
                {
                    _pendingSingleSelect = node;
                    e.Handled = true;
                }
            }
            else
            {
                _pressTreeNodeEvent = null;
                _pressedTreeNode = null;
                _startDragTreeNode = false;
            }
        }

        // Registered on the tree itself (tunnel, handled events too): once an item is pressed the pointer may be
        // captured by its ListBoxItem, so the item's own Grid would not receive move/release events.
        private void OnPointerReleasedOnTree(object _1, PointerReleasedEventArgs _2)
        {
            if (_pressTreeNodeEvent == null)
                return;

            if (_pendingSingleSelect != null && !_startDragTreeNode)
                TreeContainer.SelectedItem = _pendingSingleSelect;

            _pendingSingleSelect = null;
            _pressTreeNodeEvent = null;
            _pressedTreeNode = null;
            _startDragTreeNode = false;
        }

        private async void OnPointerMovedOverTree(object _, PointerEventArgs e)
        {
            if (_pressTreeNodeEvent != null && !_startDragTreeNode && _pressedTreeNode is { } node)
            {
                var delta = e.GetPosition(TreeContainer) - _pressTreeNodeEvent.GetPosition(TreeContainer);
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTreeNode = true;
                _pendingSingleSelect = null;

                var selected = GetSelectedNodes();
                var dragged = selected.Contains(node) ? selected : [node];
                var ids = new List<string>();
                foreach (var one in dragged)
                    ids.Add(one.Id);

                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(_dndRepoNode, string.Join('\n', ids)));
                await DragDrop.DoDragDropAsync(_pressTreeNodeEvent, data, DragDropEffects.Move);
            }
        }

        private void OnTreeViewLostFocus(object _1, RoutedEventArgs _2)
        {
            // Focus moving between the tree and its items is not a real focus loss.
            if (TreeContainer.IsKeyboardFocusWithin)
                return;

            _pendingSingleSelect = null;
            _pressTreeNodeEvent = null;
            _pressedTreeNode = null;
            _startDragTreeNode = false;
        }

        private void OnPagePointerPressed(object _, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is not Visual source)
                return;

            // Only start on empty space: not on an item, the search box, a button or a scroll bar.
            if (source.FindAncestorOfType<ListBoxItem>(true) != null ||
                source.FindAncestorOfType<ScrollBar>(true) != null ||
                source.FindAncestorOfType<TextBox>(true) != null ||
                source.FindAncestorOfType<Button>(true) != null)
                return;

            var ctrl = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            _rubberBandBase = e.KeyModifiers.HasFlag(ctrl) ? GetSelectedIndexes() : [];
            _rubberBandStartInLayer = e.GetPosition(RubberBandLayer);
            _rubberBandStartInTree = e.GetPosition(TreeContainer);
            _isRubberBanding = true;

            Canvas.SetLeft(RubberBand, _rubberBandStartInLayer.X);
            Canvas.SetTop(RubberBand, _rubberBandStartInLayer.Y);
            RubberBand.Width = 0;
            RubberBand.Height = 0;
            RubberBand.IsVisible = true;

            TreeContainer.Focus(NavigationMethod.Pointer);
            e.Pointer.Capture(this);
            ApplyRubberBandSelection(new Rect(_rubberBandStartInTree, _rubberBandStartInTree));
            e.Handled = true;
        }

        private void OnPagePointerMoved(object _, PointerEventArgs e)
        {
            if (!_isRubberBanding)
                return;

            var layerRect = new Rect(_rubberBandStartInLayer, e.GetPosition(RubberBandLayer)).Normalize();
            Canvas.SetLeft(RubberBand, layerRect.X);
            Canvas.SetTop(RubberBand, layerRect.Y);
            RubberBand.Width = layerRect.Width;
            RubberBand.Height = layerRect.Height;

            ApplyRubberBandSelection(new Rect(_rubberBandStartInTree, e.GetPosition(TreeContainer)).Normalize());
            e.Handled = true;
        }

        private void OnPagePointerReleased(object _, PointerReleasedEventArgs e)
        {
            if (!_isRubberBanding)
                return;

            e.Pointer.Capture(null);
            EndRubberBand();
            e.Handled = true;
        }

        private void EndRubberBand()
        {
            _isRubberBanding = false;
            _rubberBandBase = [];
            RubberBand.IsVisible = false;
        }

        private void ApplyRubberBandSelection(Rect rect)
        {
            var indexes = new HashSet<int>(_rubberBandBase);
            foreach (var container in TreeContainer.GetRealizedContainers())
            {
                var topLeft = container.TranslatePoint(new Point(0, 0), TreeContainer);
                if (topLeft == null)
                    continue;

                var bounds = new Rect(topLeft.Value, container.Bounds.Size);
                if (bounds.Intersects(rect))
                {
                    var idx = TreeContainer.IndexFromContainer(container);
                    if (idx >= 0)
                        indexes.Add(idx);
                }
            }

            var selection = TreeContainer.Selection;
            selection.BeginBatchUpdate();
            try
            {
                selection.Clear();
                foreach (var idx in indexes)
                    selection.Select(idx);
            }
            finally
            {
                selection.EndBatchUpdate();
            }
        }

        private List<int> GetSelectedIndexes()
        {
            var indexes = new List<int>();
            foreach (var idx in TreeContainer.Selection.SelectedIndexes)
                indexes.Add(idx);
            return indexes;
        }

        private List<ViewModels.RepositoryNode> GetSelectedNodes()
        {
            var nodes = new List<ViewModels.RepositoryNode>();
            if (TreeContainer.SelectedItems == null)
                return nodes;

            // Keep the visual order of the tree.
            foreach (var row in ViewModels.Welcome.Instance.Rows)
            {
                if (TreeContainer.SelectedItems.Contains(row))
                    nodes.Add(row);
            }

            return nodes;
        }

        private List<ViewModels.RepositoryNode> FindDraggedNodes(string data)
        {
            var nodes = new List<ViewModels.RepositoryNode>();
            foreach (var id in data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var node = ViewModels.Welcome.Instance.FindNodeById(id);
                if (node != null)
                    nodes.Add(node);
            }

            return nodes;
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
            if (e.DataTransfer.TryGetValue(_dndRepoNode) is { Length: > 1 } nodeIds)
            {
                ViewModels.Welcome.Instance.MoveNodes(FindDraggedNodes(nodeIds), null);
                e.Handled = true;
            }
            else if (e.DataTransfer.Contains(DataFormat.File))
            {
                e.Handled = true;

                var items = e.DataTransfer.TryGetFiles() ?? [];
                var refresh = false;

                foreach (var item in items)
                {
                    var root = item.Path.LocalPath;
                    if (!Directory.Exists(root))
                    {
                        if (File.Exists(root))
                            root = Path.GetDirectoryName(root);
                        else
                            continue;
                    }

                    var rs = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(root);
                    if (!rs.IsSuccess)
                    {
                        if (Models.SafeDirectories.IsUntrustedRepository(rs.StdErr) &&
                            Models.SafeDirectories.TryGetSafeDirectoryValue(root, rs.StdErr, out var safeDirectory))
                        {
                            var pageView = this.FindAncestorOfType<LauncherPage>();
                            if (pageView is { DataContext: ViewModels.LauncherPage page })
                            {
                                page.Popup = new ViewModels.TrustRepository(page.Node.Id, root, rs.StdErr, safeDirectory, null, false, false, 0);
                                break; // Stop processing further because `SourceGit` only supports processing one popup at a time.
                            }
                        }
                    }
                    else
                    {
                        await ViewModels.Welcome.Instance.AddRepositoryAsync(rs.StdOut.Trim(), null, true, false);
                        refresh = true;
                    }
                }

                if (refresh)
                    ViewModels.Welcome.Instance.Refresh();
            }

            _pressTreeNodeEvent = null;
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

            if (e.DataTransfer.TryGetValue(_dndRepoNode) is { } nodeIds)
            {
                e.Handled = true;
                ViewModels.Welcome.Instance.MoveNodes(FindDraggedNodes(nodeIds), to);
            }
            else if (e.DataTransfer.Contains(DataFormat.File))
            {
                e.Handled = true;

                var items = e.DataTransfer.TryGetFiles() ?? [];
                var refresh = false;

                foreach (var item in items)
                {
                    var root = item.Path.LocalPath;
                    if (!Directory.Exists(root))
                    {
                        if (File.Exists(root))
                            root = Path.GetDirectoryName(root);
                        else
                            continue;
                    }

                    var rs = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(root);
                    if (!rs.IsSuccess)
                    {
                        if (Models.SafeDirectories.IsUntrustedRepository(rs.StdErr) &&
                            Models.SafeDirectories.TryGetSafeDirectoryValue(root, rs.StdErr, out var safeDirectory))
                        {
                            var pageView = this.FindAncestorOfType<LauncherPage>();
                            if (pageView is { DataContext: ViewModels.LauncherPage page })
                            {
                                page.Popup = new ViewModels.TrustRepository(page.Node.Id, root, rs.StdErr, safeDirectory, to, false, false, 0);
                                break; // Stop processing further because `SourceGit` only supports processing one popup at a time.
                            }
                        }
                    }
                    else
                    {
                        await ViewModels.Welcome.Instance.AddRepositoryAsync(rs.StdOut.Trim(), to, true, false);
                        refresh = true;
                    }
                }

                if (refresh)
                    ViewModels.Welcome.Instance.Refresh();
            }

            _pressTreeNodeEvent = null;
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

        private PointerPressedEventArgs _pressTreeNodeEvent = null;
        private bool _startDragTreeNode = false;
        private ViewModels.RepositoryNode _pendingSingleSelect = null;
        private ViewModels.RepositoryNode _pressedTreeNode = null;
        private bool _isRubberBanding = false;
        private Point _rubberBandStartInLayer;
        private Point _rubberBandStartInTree;
        private List<int> _rubberBandBase = [];
        private readonly DataFormat<string> _dndRepoNode = DataFormat.CreateStringApplicationFormat("sourcegit-dnd-repo-node");
        private CancellationTokenSource _cancellation = new CancellationTokenSource();
    }
}
