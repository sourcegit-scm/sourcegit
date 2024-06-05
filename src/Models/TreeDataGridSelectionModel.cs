using System;
using System.Collections;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Input;

namespace SourceGit.Models
{
    public class TreeDataGridSelectionModel<TModel> : TreeSelectionModelBase<TModel>,
        ITreeDataGridRowSelectionModel<TModel>,
        ITreeDataGridSelectionInteraction
        where TModel : class
    {
        private static readonly Point s_InvalidPoint = new(double.NegativeInfinity, double.NegativeInfinity);

        private readonly ITreeDataGridSource<TModel> _source;
        private EventHandler _viewSelectionChanged;
        private EventHandler _rowDoubleTapped;
        private Point _pressedPoint = s_InvalidPoint;
        private bool _raiseViewSelectionChanged;
        private Func<TModel, IEnumerable<TModel>> _childrenGetter;

        public TreeDataGridSelectionModel(ITreeDataGridSource<TModel> source, Func<TModel, IEnumerable<TModel>> childrenGetter)
            : base(source.Items)
        {
            _source = source;
            _childrenGetter = childrenGetter;

            SelectionChanged += (s, e) =>
            {
                if (!IsSourceCollectionChanging)
                    _viewSelectionChanged?.Invoke(this, e);
                else
                    _raiseViewSelectionChanged = true;
            };
        }

        public void Select(IEnumerable<TModel> items)
        {
            using (BatchUpdate())
            {
                Clear();

                foreach (var selected in items)
                {
                    var idx = GetModelIndex(_source.Items, selected, IndexPath.Unselected);
                    if (!idx.Equals(IndexPath.Unselected))
                        Select(idx);
                }
            }
        }

        event EventHandler ITreeDataGridSelectionInteraction.SelectionChanged
        {
            add => _viewSelectionChanged += value;
            remove => _viewSelectionChanged -= value;
        }

        public event EventHandler RowDoubleTapped
        {
            add => _rowDoubleTapped += value;
            remove => _rowDoubleTapped -= value;
        }

        IEnumerable ITreeDataGridSelection.Source
        {
            get => Source;
            set => Source = value;
        }

        bool ITreeDataGridSelectionInteraction.IsRowSelected(IRow rowModel)
        {
            if (rowModel is IModelIndexableRow indexable)
                return IsSelected(indexable.ModelIndexPath);
            return false;
        }

        bool ITreeDataGridSelectionInteraction.IsRowSelected(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < _source.Rows.Count)
            {
                if (_source.Rows[rowIndex] is IModelIndexableRow indexable)
                    return IsSelected(indexable.ModelIndexPath);
            }

            return false;
        }

        void ITreeDataGridSelectionInteraction.OnKeyDown(TreeDataGrid sender, KeyEventArgs e)
        {
            if (sender.RowsPresenter is null)
                return;

            if (!e.Handled)
            {
                var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                if (e.Key == Key.A && ctrl && !SingleSelect)
                {
                    using (BatchUpdate())
                    {
                        Clear();

                        int num = _source.Rows.Count;
                        for (int i = 0; i < num; ++i)
                        {
                            var m = _source.Rows.RowIndexToModelIndex(i);
                            Select(m);
                        }
                    }
                    e.Handled = true;
                }

                var direction = e.Key.ToNavigationDirection();
                var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (direction.HasValue)
                {
                    var anchorRowIndex = _source.Rows.ModelIndexToRowIndex(AnchorIndex);
                    sender.RowsPresenter.BringIntoView(anchorRowIndex);

                    var anchor = sender.TryGetRow(anchorRowIndex);
                    if (anchor is not null && !ctrl)
                    {
                        e.Handled = TryKeyExpandCollapse(sender, direction.Value, anchor);
                    }

                    if (!e.Handled && (!ctrl || shift))
                    {
                        e.Handled = MoveSelection(sender, direction.Value, shift, anchor);
                    }

                    if (!e.Handled && direction == NavigationDirection.Left
                        && anchor?.Rows is HierarchicalRows<TModel> hierarchicalRows && anchorRowIndex > 0)
                    {
                        var newIndex = hierarchicalRows.GetParentRowIndex(AnchorIndex);
                        UpdateSelection(sender, newIndex, true);
                        FocusRow(sender, sender.RowsPresenter.BringIntoView(newIndex));
                    }

                    if (!e.Handled && direction == NavigationDirection.Right
                       && anchor?.Rows is HierarchicalRows<TModel> hierarchicalRows2 && hierarchicalRows2[anchorRowIndex].IsExpanded)
                    {
                        var newIndex = anchorRowIndex + 1;
                        UpdateSelection(sender, newIndex, true);
                        sender.RowsPresenter.BringIntoView(newIndex);
                    }
                }
            }
        }

        void ITreeDataGridSelectionInteraction.OnPointerPressed(TreeDataGrid sender, PointerPressedEventArgs e)
        {
            if (!e.Handled &&
                e.Pointer.Type == PointerType.Mouse &&
                e.Source is Control source &&
                sender.TryGetRow(source, out var row) &&
                _source.Rows.RowIndexToModelIndex(row.RowIndex) is { } modelIndex)
            {
                if (!IsSelected(modelIndex))
                {
                    PointerSelect(sender, row, e);
                    _pressedPoint = s_InvalidPoint;
                }
                else
                {
                    var point = e.GetCurrentPoint(sender);
                    if (point.Properties.IsRightButtonPressed)
                    {
                        _pressedPoint = s_InvalidPoint;
                        return;
                    }

                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        Deselect(modelIndex);
                    }
                    else if (e.ClickCount % 2 == 0)
                    {
                        var focus = _source.Rows[row.RowIndex];
                        if (focus is IExpander expander && HasChildren(focus))
                            expander.IsExpanded = !expander.IsExpanded;                            
                        else
                            _rowDoubleTapped?.Invoke(this, e);

                        e.Handled = true;
                    }
                    else if (sender.RowSelection.Count > 1)
                    {
                        using (BatchUpdate())
                        {
                            Clear();
                            Select(modelIndex);
                        }
                    }

                    _pressedPoint = s_InvalidPoint;
                }
            }
            else
            {
                if (!sender.TryGetRow(e.Source as Control, out var test))
                    Clear();

                _pressedPoint = e.GetPosition(sender);
            }
        }

        void ITreeDataGridSelectionInteraction.OnPointerReleased(TreeDataGrid sender, PointerReleasedEventArgs e)
        {
            if (!e.Handled &&
                _pressedPoint != s_InvalidPoint &&
                e.Source is Control source &&
                sender.TryGetRow(source, out var row) &&
                _source.Rows.RowIndexToModelIndex(row.RowIndex) is { } modelIndex)
            {
                if (!IsSelected(modelIndex))
                {
                    var p = e.GetPosition(sender);
                    if (Math.Abs(p.X - _pressedPoint.X) <= 3 || Math.Abs(p.Y - _pressedPoint.Y) <= 3)
                        PointerSelect(sender, row, e);
                }
            }
        }

        protected override void OnSourceCollectionChangeFinished()
        {
            if (_raiseViewSelectionChanged)
            {
                _viewSelectionChanged?.Invoke(this, EventArgs.Empty);
                _raiseViewSelectionChanged = false;
            }
        }

        private void PointerSelect(TreeDataGrid sender, TreeDataGridRow row, PointerEventArgs e)
        {
            var point = e.GetCurrentPoint(sender);

            var commandModifiers = TopLevel.GetTopLevel(sender)?.PlatformSettings?.HotkeyConfiguration.CommandModifiers;
            var toggleModifier = commandModifiers is not null && e.KeyModifiers.HasFlag(commandModifiers);
            var isRightButton = point.Properties.PointerUpdateKind is PointerUpdateKind.RightButtonPressed or
                PointerUpdateKind.RightButtonReleased;

            UpdateSelection(
                sender,
                row.RowIndex,
                select: true,
                rangeModifier: e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                toggleModifier: toggleModifier,
                rightButton: isRightButton);
            e.Handled = true;
        }

        private void UpdateSelection(TreeDataGrid treeDataGrid, int rowIndex, bool select = true, bool rangeModifier = false, bool toggleModifier = false, bool rightButton = false)
        {
            var modelIndex = _source.Rows.RowIndexToModelIndex(rowIndex);
            if (modelIndex == default)
                return;

            var mode = SingleSelect ? SelectionMode.Single : SelectionMode.Multiple;
            var multi = (mode & SelectionMode.Multiple) != 0;
            var toggle = (toggleModifier || (mode & SelectionMode.Toggle) != 0);
            var range = multi && rangeModifier;

            if (!select)
            {
                if (IsSelected(modelIndex) && !treeDataGrid.QueryCancelSelection())
                    Deselect(modelIndex);
            }
            else if (rightButton)
            {
                if (IsSelected(modelIndex) == false && !treeDataGrid.QueryCancelSelection())
                    SelectedIndex = modelIndex;
            }
            else if (range)
            {
                if (!treeDataGrid.QueryCancelSelection())
                {
                    var anchor = RangeAnchorIndex;
                    var i = Math.Max(_source.Rows.ModelIndexToRowIndex(anchor), 0);
                    var step = i < rowIndex ? 1 : -1;

                    using (BatchUpdate())
                    {
                        Clear();

                        while (true)
                        {
                            var m = _source.Rows.RowIndexToModelIndex(i);
                            Select(m);
                            anchor = m;
                            if (i == rowIndex)
                                break;
                            i += step;
                        }
                    }
                }
            }
            else if (multi && toggle)
            {
                if (!treeDataGrid.QueryCancelSelection())
                {
                    if (IsSelected(modelIndex) == true)
                        Deselect(modelIndex);
                    else
                        Select(modelIndex);
                }
            }
            else if (toggle)
            {
                if (!treeDataGrid.QueryCancelSelection())
                    SelectedIndex = (SelectedIndex == modelIndex) ? -1 : modelIndex;
            }
            else if (SelectedIndex != modelIndex || Count > 1)
            {
                if (!treeDataGrid.QueryCancelSelection())
                    SelectedIndex = modelIndex;
            }
        }

        private bool TryKeyExpandCollapse(TreeDataGrid treeDataGrid, NavigationDirection direction, TreeDataGridRow focused)
        {
            if (treeDataGrid.RowsPresenter is null || focused.RowIndex < 0)
                return false;

            var row = _source.Rows[focused.RowIndex];

            if (row is IExpander expander)
            {
                if (direction == NavigationDirection.Right && !expander.IsExpanded)
                {
                    expander.IsExpanded = true;
                    return true;
                }
                else if (direction == NavigationDirection.Left && expander.IsExpanded)
                {
                    expander.IsExpanded = false;
                    return true;
                }
            }

            return false;
        }

        private bool MoveSelection(TreeDataGrid treeDataGrid, NavigationDirection direction, bool rangeModifier, TreeDataGridRow focused)
        {
            if (treeDataGrid.RowsPresenter is null || _source.Columns.Count == 0 || _source.Rows.Count == 0)
                return false;

            var currentRowIndex = focused?.RowIndex ?? _source.Rows.ModelIndexToRowIndex(SelectedIndex);
            int newRowIndex;

            if (direction == NavigationDirection.First || direction == NavigationDirection.Last)
            {
                newRowIndex = direction == NavigationDirection.First ? 0 : _source.Rows.Count - 1;
            }
            else
            {
                (var x, var y) = direction switch
                {
                    NavigationDirection.Up => (0, -1),
                    NavigationDirection.Down => (0, 1),
                    NavigationDirection.Left => (-1, 0),
                    NavigationDirection.Right => (1, 0),
                    _ => (0, 0)
                };

                newRowIndex = Math.Max(0, Math.Min(currentRowIndex + y, _source.Rows.Count - 1));
            }

            if (newRowIndex != currentRowIndex)
                UpdateSelection(treeDataGrid, newRowIndex, true, rangeModifier);

            if (newRowIndex != currentRowIndex)
            {
                treeDataGrid.RowsPresenter?.BringIntoView(newRowIndex);
                FocusRow(treeDataGrid, treeDataGrid.TryGetRow(newRowIndex));
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void FocusRow(TreeDataGrid owner, Control control)
        {
            if (!owner.TryGetRow(control, out var row) || row.CellsPresenter is null)
                return;

            // Get the column index of the currently focused cell if possible: we'll try to focus the
            // same column in the new row.
            if (TopLevel.GetTopLevel(owner)?.FocusManager is { } focusManager &&
                focusManager.GetFocusedElement() is Control currentFocus &&
                owner.TryGetCell(currentFocus, out var currentCell) &&
                row.TryGetCell(currentCell.ColumnIndex) is { } newCell &&
                newCell.Focusable)
            {
                newCell.Focus();
            }
            else
            {
                // Otherwise, just focus the first focusable cell in the row.
                foreach (var cell in row.CellsPresenter.GetRealizedElements())
                {
                    if (cell.Focusable)
                    {
                        cell.Focus();
                        break;
                    }
                }
            }
        }

        protected override IEnumerable<TModel> GetChildren(TModel node)
        {
            if (node == null)
                return null;

            return _childrenGetter?.Invoke(node);
        }

        private IndexPath GetModelIndex(IEnumerable<TModel> collection, TModel model, IndexPath parent)
        {
            int i = 0;

            foreach (var item in collection)
            {
                var index = parent.Append(i);
                if (item != null && item == model)
                    return index;

                var children = GetChildren(item);
                if (children != null)
                {
                    var findInChildren = GetModelIndex(children, model, index);
                    if (!findInChildren.Equals(IndexPath.Unselected))
                        return findInChildren;
                }

                i++;
            }

            return IndexPath.Unselected;
        }

        private bool HasChildren(IRow row)
        {
            var children = GetChildren(row.Model as TModel);
            if (children != null)
            {
                foreach (var c in children)
                    return true;
            }
            
            return false;
        }
    }
}
