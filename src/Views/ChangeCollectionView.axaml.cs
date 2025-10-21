using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class ChangeTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.ChangeTreeNode { IsFolder: true } node)
            {
                var tree = this.FindAncestorOfType<ChangeCollectionView>();
                tree?.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class ChangeCollectionContainer : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (SelectedItems is [ViewModels.ChangeTreeNode node])
            {
                if (((e.Key == Key.Left && node.IsExpanded) || (e.Key == Key.Right && !node.IsExpanded)) &&
                    e.KeyModifiers == KeyModifiers.None)
                {
                    this.FindAncestorOfType<ChangeCollectionView>()?.ToggleNodeIsExpanded(node);
                    e.Handled = true;
                }
            }

            if (!e.Handled && e.Key != Key.Space && e.Key != Key.Enter)
                base.OnKeyDown(e);
        }
    }

    public partial class ChangeCollectionView : UserControl
    {
        public static readonly StyledProperty<bool> IsUnstagedChangeProperty =
            AvaloniaProperty.Register<ChangeCollectionView, bool>(nameof(IsUnstagedChange));

        public bool IsUnstagedChange
        {
            get => GetValue(IsUnstagedChangeProperty);
            set => SetValue(IsUnstagedChangeProperty, value);
        }

        public static readonly StyledProperty<Models.ChangeViewMode> ViewModeProperty =
            AvaloniaProperty.Register<ChangeCollectionView, Models.ChangeViewMode>(nameof(ViewMode), Models.ChangeViewMode.Tree);

        public Models.ChangeViewMode ViewMode
        {
            get => GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        public static readonly StyledProperty<bool> EnableCompactFoldersProperty =
            AvaloniaProperty.Register<ChangeCollectionView, bool>(nameof(EnableCompactFolders));

        public bool EnableCompactFolders
        {
            get => GetValue(EnableCompactFoldersProperty);
            set => SetValue(EnableCompactFoldersProperty, value);
        }

        public static readonly StyledProperty<List<Models.Change>> ChangesProperty =
            AvaloniaProperty.Register<ChangeCollectionView, List<Models.Change>>(nameof(Changes));

        public List<Models.Change> Changes
        {
            get => GetValue(ChangesProperty);
            set => SetValue(ChangesProperty, value);
        }

        public static readonly StyledProperty<List<Models.Change>> SelectedChangesProperty =
            AvaloniaProperty.Register<ChangeCollectionView, List<Models.Change>>(nameof(SelectedChanges));

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

        public void ToggleNodeIsExpanded(ViewModels.ChangeTreeNode node)
        {
            if (Content is ViewModels.ChangeCollectionAsTree tree && node.IsFolder)
            {
                node.IsExpanded = !node.IsExpanded;

                var depth = node.Depth;
                var idx = tree.Rows.IndexOf(node);
                if (idx == -1)
                    return;

                if (node.IsExpanded)
                {
                    var subrows = new List<ViewModels.ChangeTreeNode>();
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

        public Models.Change GetNextChangeWithoutSelection()
        {
            var selected = SelectedChanges;
            var changes = Changes;
            if (selected == null || selected.Count == 0)
                return changes.Count > 0 ? changes[0] : null;
            if (selected.Count == changes.Count)
                return null;

            var set = new HashSet<string>();
            foreach (var c in selected)
            {
                if (!c.IsConflicted)
                    set.Add(c.Path);
            }

            if (Content is ViewModels.ChangeCollectionAsTree tree)
            {
                var lastUnselected = -1;
                for (int i = tree.Rows.Count - 1; i >= 0; i--)
                {
                    var row = tree.Rows[i];
                    if (!row.IsFolder)
                    {
                        if (set.Contains(row.FullPath))
                        {
                            if (lastUnselected == -1)
                                continue;

                            break;
                        }

                        lastUnselected = i;
                    }
                }

                if (lastUnselected != -1)
                    return tree.Rows[lastUnselected].Change;
            }
            else
            {
                var lastUnselected = -1;
                for (int i = changes.Count - 1; i >= 0; i--)
                {
                    if (set.Contains(changes[i].Path))
                    {
                        if (lastUnselected == -1)
                            continue;

                        break;
                    }

                    lastUnselected = i;
                }

                if (lastUnselected != -1)
                    return changes[lastUnselected];
            }

            return null;
        }

        public void TakeFocus()
        {
            var container = this.FindDescendantOfType<ChangeCollectionContainer>();
            if (container is { IsFocused: false })
                container.Focus();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ViewModeProperty)
                UpdateDataSource(true);
            else if (change.Property == ChangesProperty)
                UpdateDataSource(false);
            else if (change.Property == SelectedChangesProperty)
                UpdateSelection();

            if (change.Property == EnableCompactFoldersProperty && ViewMode == Models.ChangeViewMode.Tree)
                UpdateDataSource(true);
        }

        private void OnRowDataContextChanged(object sender, EventArgs e)
        {
            if (sender is not Control control)
                return;

            if (control.DataContext is ViewModels.ChangeTreeNode node)
            {
                if (node.Change is { } c)
                    UpdateRowTips(control, c);
                else
                    ToolTip.SetTip(control, node.FullPath);
            }
            else if (control.DataContext is Models.Change change)
            {
                UpdateRowTips(control, change);
            }
            else
            {
                ToolTip.SetTip(control, null);
            }
        }

        private void OnRowDoubleTapped(object sender, TappedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid?.DataContext is ViewModels.ChangeTreeNode node)
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
            else if (grid?.DataContext is Models.Change)
            {
                RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
            }
        }

        private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            if (_disableSelectionChangingEvent)
                return;

            _disableSelectionChangingEvent = true;

            var selected = new List<Models.Change>();
            if (sender is ListBox { SelectedItems: { } selectedItems })
            {
                foreach (var item in selectedItems)
                {
                    if (item is Models.Change c)
                        selected.Add(c);
                    else if (item is ViewModels.ChangeTreeNode node)
                        CollectChangesInNode(selected, node);
                }
            }

            var old = SelectedChanges ?? [];
            if (old.Count != selected.Count)
            {
                SetCurrentValue(SelectedChangesProperty, selected);
            }
            else
            {
                bool allEquals = true;
                foreach (var c in old)
                {
                    if (!selected.Contains(c))
                    {
                        allEquals = false;
                        break;
                    }
                }

                if (!allEquals)
                    SetCurrentValue(SelectedChangesProperty, selected);
            }

            _disableSelectionChangingEvent = false;
        }

        private void MakeTreeRows(List<ViewModels.ChangeTreeNode> rows, List<ViewModels.ChangeTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                rows.Add(node);

                if (!node.IsExpanded || !node.IsFolder)
                    continue;

                MakeTreeRows(rows, node.Children);
            }
        }

        private void UpdateDataSource(bool onlyViewModeChange)
        {
            _disableSelectionChangingEvent = !onlyViewModeChange;

            var changes = Changes;
            if (changes == null || changes.Count == 0)
            {
                Content = null;
                _disableSelectionChangingEvent = false;
                return;
            }

            var selected = SelectedChanges ?? [];
            if (ViewMode == Models.ChangeViewMode.Tree)
            {
                HashSet<string> oldFolded = new HashSet<string>();
                if (Content is ViewModels.ChangeCollectionAsTree oldTree)
                {
                    foreach (var row in oldTree.Rows)
                    {
                        if (row.IsFolder && !row.IsExpanded)
                            oldFolded.Add(row.FullPath);
                    }
                }

                var tree = new ViewModels.ChangeCollectionAsTree();
                tree.Tree = ViewModels.ChangeTreeNode.Build(changes, oldFolded, EnableCompactFolders);

                var rows = new List<ViewModels.ChangeTreeNode>();
                MakeTreeRows(rows, tree.Tree);
                tree.Rows.AddRange(rows);

                if (selected.Count > 0)
                {
                    var sets = new HashSet<Models.Change>(selected);
                    var nodes = new List<ViewModels.ChangeTreeNode>();
                    foreach (var row in tree.Rows)
                    {
                        if (row.Change != null && sets.Contains(row.Change))
                            nodes.Add(row);
                    }

                    tree.SelectedRows.AddRange(nodes);
                }

                Content = tree;
            }
            else if (ViewMode == Models.ChangeViewMode.Grid)
            {
                var grid = new ViewModels.ChangeCollectionAsGrid();
                grid.Changes.AddRange(changes);
                if (selected.Count > 0)
                    grid.SelectedChanges.AddRange(selected);

                Content = grid;
            }
            else
            {
                var list = new ViewModels.ChangeCollectionAsList();
                list.Changes.AddRange(changes);
                if (selected.Count > 0)
                    list.SelectedChanges.AddRange(selected);

                Content = list;
            }

            _disableSelectionChangingEvent = false;
        }

        private void UpdateSelection()
        {
            if (_disableSelectionChangingEvent)
                return;

            _disableSelectionChangingEvent = true;

            var selected = SelectedChanges ?? [];
            if (Content is ViewModels.ChangeCollectionAsTree tree)
            {
                tree.SelectedRows.Clear();

                if (selected.Count > 0)
                {
                    var sets = new HashSet<Models.Change>(selected);

                    var nodes = new List<ViewModels.ChangeTreeNode>();
                    foreach (var row in tree.Rows)
                    {
                        if (row.Change != null && sets.Contains(row.Change))
                            nodes.Add(row);
                    }

                    tree.SelectedRows.AddRange(nodes);
                }
            }
            else if (Content is ViewModels.ChangeCollectionAsGrid grid)
            {
                grid.SelectedChanges.Clear();
                if (selected.Count > 0)
                    grid.SelectedChanges.AddRange(selected);
            }
            else if (Content is ViewModels.ChangeCollectionAsList list)
            {
                list.SelectedChanges.Clear();
                if (selected.Count > 0)
                    list.SelectedChanges.AddRange(selected);
            }

            _disableSelectionChangingEvent = false;
        }

        private void CollectChangesInNode(List<Models.Change> outs, ViewModels.ChangeTreeNode node)
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

        private void UpdateRowTips(Control control, Models.Change change)
        {
            var tip = new TextBlock() { TextWrapping = TextWrapping.Wrap };
            tip.Inlines!.Add(new Run(change.Path));
            tip.Inlines!.Add(new Run(" • ") { Foreground = Brushes.Gray });
            tip.Inlines!.Add(new Run(IsUnstagedChange ? change.WorkTreeDesc : change.IndexDesc) { Foreground = Brushes.Gray });
            if (change.IsConflicted)
            {
                tip.Inlines!.Add(new Run(" • ") { Foreground = Brushes.Gray });
                tip.Inlines!.Add(new Run(change.ConflictDesc) { Foreground = Brushes.Gray });
            }

            ToolTip.SetTip(control, tip);
        }

        private bool _disableSelectionChangingEvent = false;
    }
}
