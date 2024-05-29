using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ChangeCollectionView : UserControl
    {
        public static readonly StyledProperty<bool> IsWorkingCopyChangeProperty =
            AvaloniaProperty.Register<ChangeCollectionView, bool>(nameof(IsWorkingCopy), false);

        public bool IsWorkingCopy
        {
            get => GetValue(IsWorkingCopyChangeProperty);
            set => SetValue(IsWorkingCopyChangeProperty, value);
        }

        public static readonly StyledProperty<bool> SingleSelectProperty =
            AvaloniaProperty.Register<ChangeCollectionView, bool>(nameof(SingleSelect), true);

        public bool SingleSelect
        {
            get => GetValue(SingleSelectProperty);
            set => SetValue(SingleSelectProperty, value);
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

        static ChangeCollectionView()
        {
            ViewModeProperty.Changed.AddClassHandler<ChangeCollectionView>((c, e) => c.UpdateSource());
            ChangesProperty.Changed.AddClassHandler<ChangeCollectionView>((c, e) => c.UpdateSource());
            SelectedChangesProperty.Changed.AddClassHandler<ChangeCollectionView>((c, e) => c.UpdateSelected());
        }

        public ChangeCollectionView()
        {
            InitializeComponent();
        }

        private void UpdateSource()
        {
            if (tree.Source is IDisposable disposable)
            {
                disposable.Dispose();
                tree.Source = null;
            }

            var changes = Changes;
            if (changes == null)
                return;

            var viewMode = ViewMode;
            if (viewMode == Models.ChangeViewMode.Tree)
            {
                var filetree = ViewModels.FileTreeNode.Build(changes, true);
                var source = new HierarchicalTreeDataGridSource<ViewModels.FileTreeNode>(filetree)
                {
                    Columns =
                    {
                        new HierarchicalExpanderColumn<ViewModels.FileTreeNode>(
                            new TemplateColumn<ViewModels.FileTreeNode>(null, "TreeModeTemplate", null, GridLength.Auto),
                            x => x.Children,
                            x => x.Children.Count > 0,
                            x => x.IsExpanded),
                        new TextColumn<ViewModels.FileTreeNode, string>(
                            null,
                            x => string.Empty,
                            GridLength.Star)
                    }
                };

                var selection = new Models.TreeDataGridSelectionModel<ViewModels.FileTreeNode>(source, x => x.Children);
                selection.SingleSelect = SingleSelect;
                selection.RowDoubleTapped += (_, e) => RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
                selection.SelectionChanged += (s, _) =>
                {
                    if (!_isSelecting && s is Models.TreeDataGridSelectionModel<ViewModels.FileTreeNode> model)
                    {
                        var selection = new List<Models.Change>();
                        foreach (var c in model.SelectedItems)
                            CollectChangesInNode(selection, c);

                        _isSelecting = true;
                        SetCurrentValue(SelectedChangesProperty, selection);
                        _isSelecting = false;
                    }
                };

                source.Selection = selection;
                tree.Source = source;
            }
            else if (viewMode == Models.ChangeViewMode.List)
            {
                var source = new FlatTreeDataGridSource<Models.Change>(changes)
                {
                    Columns = { new TemplateColumn<Models.Change>(null, "ListModeTemplate", null, GridLength.Auto) }
                };

                var selection = new Models.TreeDataGridSelectionModel<Models.Change>(source, null);
                selection.SingleSelect = SingleSelect;
                selection.RowDoubleTapped += (_, e) => RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
                selection.SelectionChanged += (s, _) =>
                {
                    if (!_isSelecting && s is Models.TreeDataGridSelectionModel<Models.Change> model)
                    {
                        var selection = new List<Models.Change>();
                        foreach (var c in model.SelectedItems)
                            selection.Add(c);

                        _isSelecting = true;
                        SetCurrentValue(SelectedChangesProperty, selection);
                        _isSelecting = false;
                    }
                };

                source.Selection = selection;
                tree.Source = source;
            }
            else
            {
                var source = new FlatTreeDataGridSource<Models.Change>(changes)
                {
                    Columns =
                    {
                        new TemplateColumn<Models.Change>(null, "GridModeFileTemplate", null, GridLength.Auto),
                        new TemplateColumn<Models.Change>(null, "GridModeDirTemplate", null, GridLength.Auto)
                    },
                };

                var selection = new Models.TreeDataGridSelectionModel<Models.Change>(source, null);
                selection.SingleSelect = SingleSelect;
                selection.RowDoubleTapped += (_, e) => RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
                selection.SelectionChanged += (s, _) =>
                {
                    if (!_isSelecting && s is Models.TreeDataGridSelectionModel<Models.Change> model)
                    {
                        var selection = new List<Models.Change>();
                        foreach (var c in model.SelectedItems)
                            selection.Add(c);

                        _isSelecting = true;
                        SetCurrentValue(SelectedChangesProperty, selection);
                        _isSelecting = false;
                    }
                };

                source.Selection = selection;
                tree.Source = source;
            }
        }

        private void UpdateSelected()
        {
            if (_isSelecting || tree.Source == null)
                return;

            _isSelecting = true;
            var selected = SelectedChanges;
            if (tree.Source.Selection is Models.TreeDataGridSelectionModel<Models.Change> changeSelection)
            {
                if (selected == null || selected.Count == 0)
                    changeSelection.Clear();
                else
                    changeSelection.Select(selected);
            }
            else if (tree.Source.Selection is Models.TreeDataGridSelectionModel<ViewModels.FileTreeNode> treeSelection)
            {
                if (selected == null || selected.Count == 0)
                {
                    treeSelection.Clear();
                    _isSelecting = false;
                    return;
                }

                var set = new HashSet<object>();
                foreach (var c in selected)
                    set.Add(c);

                var nodes = new List<ViewModels.FileTreeNode>();
                foreach (var node in tree.Source.Items)
                    CollectSelectedNodeByChange(nodes, node as ViewModels.FileTreeNode, set);

                if (nodes.Count == 0)
                {
                    treeSelection.Clear();
                }
                else
                {
                    treeSelection.Select(nodes);
                }
            }
            _isSelecting = false;
        }

        private void CollectChangesInNode(List<Models.Change> outs, ViewModels.FileTreeNode node)
        {
            if (node.IsFolder)
            {
                foreach (var child in node.Children)
                    CollectChangesInNode(outs, child);
            }
            else
            {
                var change = node.Backend as Models.Change;
                if (change != null && !outs.Contains(change))
                    outs.Add(change);
            }
        }

        private void CollectSelectedNodeByChange(List<ViewModels.FileTreeNode> outs, ViewModels.FileTreeNode node, HashSet<object> selected)
        {
            if (node == null)
                return;

            if (node.IsFolder)
            {
                foreach (var child in node.Children)
                    CollectSelectedNodeByChange(outs, child, selected);
            }
            else if (node.Backend != null && selected.Contains(node.Backend))
            {
                outs.Add(node);
            }
        }

        private bool _isSelecting = false;
    }
}
