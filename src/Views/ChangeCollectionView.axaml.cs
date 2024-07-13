using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Views
{
    public class ChangeTreeNode : ObservableObject
    {
        public string FullPath { get; set; } = string.Empty;
        public int Depth { get; private set; } = 0;
        public Models.Change Change { get; set; } = null;
        public List<ChangeTreeNode> Children { get; set; } = new List<ChangeTreeNode>();

        public bool IsFolder
        {
            get => Change == null;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ChangeTreeNode(Models.Change c, int depth)
        {
            FullPath = c.Path;
            Depth = depth;
            Change = c;
            IsExpanded = false;
        }

        public ChangeTreeNode(string path, bool isExpanded, int depth)
        {
            FullPath = path;
            Depth = depth;
            IsExpanded = isExpanded;
        }

        public static List<ChangeTreeNode> Build(IList<Models.Change> changes, HashSet<string> folded)
        {
            var nodes = new List<ChangeTreeNode>();
            var folders = new Dictionary<string, ChangeTreeNode>();

            foreach (var c in changes)
            {
                var sepIdx = c.Path.IndexOf('/', StringComparison.Ordinal);
                if (sepIdx == -1)
                {
                    nodes.Add(new ChangeTreeNode(c, 0));
                }
                else
                {
                    ChangeTreeNode lastFolder = null;
                    var start = 0;
                    var depth = 0;

                    while (sepIdx != -1)
                    {
                        var folder = c.Path.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new ChangeTreeNode(folder, !folded.Contains(folder), depth);
                            folders.Add(folder, lastFolder);
                            InsertFolder(nodes, lastFolder);
                        }
                        else
                        {
                            var cur = new ChangeTreeNode(folder, !folded.Contains(folder), depth);
                            folders.Add(folder, cur);
                            InsertFolder(lastFolder.Children, cur);
                            lastFolder = cur;
                        }

                        start = sepIdx + 1;
                        depth++;
                        sepIdx = c.Path.IndexOf('/', start);
                    }

                    lastFolder.Children.Add(new ChangeTreeNode(c, depth));
                }
            }

            Sort(nodes);

            folders.Clear();
            return nodes;
        }

        private static void InsertFolder(List<ChangeTreeNode> collection, ChangeTreeNode subFolder)
        {
            for (int i = 0; i < collection.Count; i++)
            {
                if (!collection[i].IsFolder)
                {
                    collection.Insert(i, subFolder);
                    return;
                }
            }

            collection.Add(subFolder);
        }

        private static void Sort(List<ChangeTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsFolder)
                    Sort(node.Children);
            }

            nodes.Sort((l, r) =>
            {
                if (l.IsFolder)
                    return r.IsFolder ? string.Compare(l.FullPath, r.FullPath, StringComparison.Ordinal) : -1;
                return r.IsFolder ? 1 : string.Compare(l.FullPath, r.FullPath, StringComparison.Ordinal);
            });
        }

        private bool _isExpanded = true;
    }

    public class ChangeCollectionAsTree
    {
        public List<ChangeTreeNode> Tree { get; set; } = new List<ChangeTreeNode>();
        public AvaloniaList<ChangeTreeNode> Rows { get; set; } = new AvaloniaList<ChangeTreeNode>();
    }

    public class ChangeCollectionAsGrid
    {
        public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    }

    public class ChangeCollectionAsList
    {
        public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    }

    public class ChangeTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ChangeTreeNode { IsFolder: true } node)
            {
                var tree = this.FindAncestorOfType<ChangeCollectionView>();
                tree.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class ChangeCollectionContainer : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key != Key.Space)
                base.OnKeyDown(e);
        }
    }

    public partial class ChangeCollectionView : UserControl
    {
        public static readonly StyledProperty<bool> IsWorkingCopyChangeProperty =
            AvaloniaProperty.Register<ChangeCollectionView, bool>(nameof(IsWorkingCopyChange), false);

        public bool IsWorkingCopyChange
        {
            get => GetValue(IsWorkingCopyChangeProperty);
            set => SetValue(IsWorkingCopyChangeProperty, value);
        }

        public static readonly StyledProperty<SelectionMode> SelectionModeProperty =
            AvaloniaProperty.Register<ChangeCollectionView, SelectionMode>(nameof(SelectionMode), SelectionMode.Single);

        public SelectionMode SelectionMode
        {
            get => GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public static readonly StyledProperty<Models.ChangeViewMode> ViewModeProperty =
            AvaloniaProperty.Register<ChangeCollectionView, Models.ChangeViewMode>(nameof(ViewMode), Models.ChangeViewMode.Tree);

        public Models.ChangeViewMode ViewMode
        {
            get => GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        public static readonly StyledProperty<List<Models.Change>> ChangesProperty =
            AvaloniaProperty.Register<ChangeCollectionView, List<Models.Change>>(nameof(Changes), null);

        public List<Models.Change> Changes
        {
            get => GetValue(ChangesProperty);
            set => SetValue(ChangesProperty, value);
        }

        public static readonly StyledProperty<List<Models.Change>> SelectedChangesProperty =
            AvaloniaProperty.Register<ChangeCollectionView, List<Models.Change>>(nameof(SelectedChanges), null);

        public List<Models.Change> SelectedChanges
        {
            get => GetValue(SelectedChangesProperty);
            set => SetValue(SelectedChangesProperty, value);
        }

        public static readonly RoutedEvent<RoutedEventArgs> ChangeDoubleTappedEvent =
            RoutedEvent.Register<ChangeCollectionView, RoutedEventArgs>(nameof(ChangeDoubleTapped), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> ChangeDoubleTapped
        {
            add { AddHandler(ChangeDoubleTappedEvent, value); }
            remove { RemoveHandler(ChangeDoubleTappedEvent, value); }
        }

        public ChangeCollectionView()
        {
            InitializeComponent();
        }

        public void ToggleNodeIsExpanded(ChangeTreeNode node)
        {
            if (_displayContext is ChangeCollectionAsTree tree)
            {
                node.IsExpanded = !node.IsExpanded;

                var depth = node.Depth;
                var idx = tree.Rows.IndexOf(node);
                if (idx == -1)
                    return;

                if (node.IsExpanded)
                {
                    var subrows = new List<ChangeTreeNode>();
                    MakeTreeRows(subrows, node.Children);
                    tree.Rows.InsertRange(idx + 1, subrows);
                }
                else
                {
                    var removeCount = 0;
                    for (int i = idx + 1; i < tree.Rows.Count; i++)
                    {
                        var row = tree.Rows[i];
                        if (row.Depth <= depth)
                            break;

                        removeCount++;
                    }
                    tree.Rows.RemoveRange(idx + 1, removeCount);
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ViewModeProperty || change.Property == ChangesProperty)
            {
                _disableSelectionChangingEvent = change.Property == ChangesProperty;
                var changes = Changes;
                if (changes == null || changes.Count == 0)
                {
                    Content = null;
                    _displayContext = null;
                    _disableSelectionChangingEvent = false;
                    return;
                }

                if (ViewMode == Models.ChangeViewMode.Tree)
                {
                    HashSet<string> oldFolded = new HashSet<string>();
                    if (_displayContext is ChangeCollectionAsTree oldTree)
                    {
                        foreach (var row in oldTree.Rows)
                        {
                            if (row.IsFolder && !row.IsExpanded)
                                oldFolded.Add(row.FullPath);
                        }
                    }

                    var tree = new ChangeCollectionAsTree();
                    tree.Tree = ChangeTreeNode.Build(changes, oldFolded);

                    var rows = new List<ChangeTreeNode>();
                    MakeTreeRows(rows, tree.Tree);
                    tree.Rows.AddRange(rows);
                    _displayContext = tree;
                }
                else if (ViewMode == Models.ChangeViewMode.Grid)
                {
                    var grid = new ChangeCollectionAsGrid();
                    grid.Changes.AddRange(changes);
                    _displayContext = grid;
                }
                else
                {
                    var list = new ChangeCollectionAsList();
                    list.Changes.AddRange(changes);
                    _displayContext = list;
                }

                Content = _displayContext;
                _disableSelectionChangingEvent = false;
            }
            else if (change.Property == SelectedChangesProperty)
            {
                if (_disableSelectionChangingEvent)
                    return;

                var list = this.FindDescendantOfType<ChangeCollectionContainer>();
                if (list == null)
                    return;

                _disableSelectionChangingEvent = true;

                var selected = SelectedChanges;
                if (selected == null || selected.Count == 0)
                {
                    list.SelectedItem = null;
                }
                else if (_displayContext is ChangeCollectionAsTree tree)
                {
                    var sets = new HashSet<Models.Change>();
                    foreach (var c in selected)
                        sets.Add(c);

                    var nodes = new List<ChangeTreeNode>();
                    foreach (var row in tree.Rows)
                    {
                        if (row.Change != null && sets.Contains(row.Change))
                            nodes.Add(row);
                    }

                    list.SelectedItems = nodes;
                }
                else
                {
                    list.SelectedItems = selected;
                }

                _disableSelectionChangingEvent = false;
            }
        }

        private void OnRowDoubleTapped(object sender, TappedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid.DataContext is ChangeTreeNode node)
            {
                if (node.IsFolder)
                {
                    var posX = e.GetPosition(this).X;
                    if (posX < node.Depth * 16 + 16)
                        return;

                    ToggleNodeIsExpanded(node);
                }
                else
                {
                    RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
                }
            }
            else if (grid.DataContext is Models.Change)
            {
                RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
            }
        }

        private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_disableSelectionChangingEvent)
                return;

            _disableSelectionChangingEvent = true;
            var selected = new List<Models.Change>();
            var list = sender as ListBox;
            foreach (var item in list.SelectedItems)
            {
                if (item is Models.Change c)
                    selected.Add(c);
                else if (item is ChangeTreeNode node)
                    CollectChangesInNode(selected, node);
            }
            TrySetSelected(selected);
            _disableSelectionChangingEvent = false;
        }

        private void MakeTreeRows(List<ChangeTreeNode> rows, List<ChangeTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                rows.Add(node);

                if (!node.IsExpanded || !node.IsFolder)
                    continue;

                MakeTreeRows(rows, node.Children);
            }
        }

        private void CollectChangesInNode(List<Models.Change> outs, ChangeTreeNode node)
        {
            if (node.IsFolder)
            {
                foreach (var child in node.Children)
                    CollectChangesInNode(outs, child);
            }
            else if (!outs.Contains(node.Change))
            {
                outs.Add(node.Change);
            }
        }

        private void TrySetSelected(List<Models.Change> changes)
        {
            var old = SelectedChanges;
            if (old == null && changes.Count == 0)
                return;

            if (old != null && old.Count == changes.Count)
            {
                bool allEquals = true;
                foreach (var c in old)
                {
                    if (!changes.Contains(c))
                    {
                        allEquals = false;
                        break;
                    }
                }

                if (allEquals)
                    return;
            }

            _disableSelectionChangingEvent = true;
            SetCurrentValue(SelectedChangesProperty, changes);
            _disableSelectionChangingEvent = false;
        }

        private bool _disableSelectionChangingEvent = false;
        private object _displayContext = null;
    }
}
