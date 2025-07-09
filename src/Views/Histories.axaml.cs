using System;
using System.Collections.Generic;
using System.Text;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class HistoriesLayout : Grid
    {
        public static readonly StyledProperty<bool> UseHorizontalProperty =
            AvaloniaProperty.Register<HistoriesLayout, bool>(nameof(UseHorizontal));

        public bool UseHorizontal
        {
            get => GetValue(UseHorizontalProperty);
            set => SetValue(UseHorizontalProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Grid);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseHorizontalProperty && IsLoaded)
                RefreshLayout();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (UseHorizontal)
            {
                var rowSpan = RowDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, 0);
                    child.SetValue(RowSpanProperty, rowSpan);
                    child.SetValue(ColumnProperty, i);
                    child.SetValue(ColumnSpanProperty, 1);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(1, 0, 0, 0);
                }
            }
            else
            {
                var colSpan = ColumnDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, i);
                    child.SetValue(RowSpanProperty, 1);
                    child.SetValue(ColumnProperty, 0);
                    child.SetValue(ColumnSpanProperty, colSpan);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(0, 1, 0, 0);
                }
            }
        }
    }

    public partial class Histories : UserControl
    {
        public static readonly StyledProperty<Models.Branch> CurrentBranchProperty =
            AvaloniaProperty.Register<Histories, Models.Branch>(nameof(CurrentBranch));

        public Models.Branch CurrentBranch
        {
            get => GetValue(CurrentBranchProperty);
            set => SetValue(CurrentBranchProperty, value);
        }

        public static readonly StyledProperty<Models.Bisect> BisectProperty =
            AvaloniaProperty.Register<Histories, Models.Bisect>(nameof(Bisect));

        public Models.Bisect Bisect
        {
            get => GetValue(BisectProperty);
            set => SetValue(BisectProperty, value);
        }

        public static readonly StyledProperty<AvaloniaList<Models.IssueTrackerRule>> IssueTrackerRulesProperty =
            AvaloniaProperty.Register<Histories, AvaloniaList<Models.IssueTrackerRule>>(nameof(IssueTrackerRules));

        public AvaloniaList<Models.IssueTrackerRule> IssueTrackerRules
        {
            get => GetValue(IssueTrackerRulesProperty);
            set => SetValue(IssueTrackerRulesProperty, value);
        }

        public static readonly StyledProperty<bool> OnlyHighlightCurrentBranchProperty =
            AvaloniaProperty.Register<Histories, bool>(nameof(OnlyHighlightCurrentBranch), true);

        public bool OnlyHighlightCurrentBranch
        {
            get => GetValue(OnlyHighlightCurrentBranchProperty);
            set => SetValue(OnlyHighlightCurrentBranchProperty, value);
        }

        public static readonly StyledProperty<long> NavigationIdProperty =
            AvaloniaProperty.Register<Histories, long>(nameof(NavigationId));

        public long NavigationId
        {
            get => GetValue(NavigationIdProperty);
            set => SetValue(NavigationIdProperty, value);
        }

        public Histories()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == NavigationIdProperty)
            {
                if (CommitListContainer is { SelectedItems.Count: 1, IsLoaded: true } dataGrid)
                    dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
            }
        }

        private void OnCommitListLoaded(object sender, RoutedEventArgs e)
        {
            var dataGrid = CommitListContainer;
            var rowsPresenter = dataGrid.FindDescendantOfType<DataGridRowsPresenter>();
            if (rowsPresenter is { Children: { Count: > 0 } rows })
                CommitGraph.Layout = new(0, dataGrid.Columns[0].ActualWidth - 4, rows[0].Bounds.Height);

            if (dataGrid.SelectedItems.Count == 1)
                dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
        }

        private void OnCommitListLayoutUpdated(object _1, EventArgs _2)
        {
            if (!IsLoaded)
                return;

            var dataGrid = CommitListContainer;
            var rowsPresenter = dataGrid.FindDescendantOfType<DataGridRowsPresenter>();
            if (rowsPresenter == null)
                return;

            double rowHeight = dataGrid.RowHeight;
            double startY = 0;
            foreach (var child in rowsPresenter.Children)
            {
                if (child is DataGridRow { IsVisible: true } row)
                {
                    rowHeight = row.Bounds.Height;

                    if (row.Bounds.Top <= 0 && row.Bounds.Top > -rowHeight)
                    {
                        var test = rowHeight * row.Index - row.Bounds.Top;
                        if (startY < test)
                            startY = test;
                    }
                }
            }

            var clipWidth = dataGrid.Columns[0].ActualWidth - 4;
            if (_lastGraphStartY != startY ||
                _lastGraphClipWidth != clipWidth ||
                _lastGraphRowHeight != rowHeight)
            {
                _lastGraphStartY = startY;
                _lastGraphClipWidth = clipWidth;
                _lastGraphRowHeight = rowHeight;

                CommitGraph.Layout = new(startY, clipWidth, rowHeight);
            }
        }

        private void OnCommitListSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories)
                histories.Select(CommitListContainer.SelectedItems);

            e.Handled = true;
        }

        private void OnCommitListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories &&
                sender is DataGrid { SelectedItems: { } selected } dataGrid &&
                e.Source is Control { DataContext: Models.Commit })
            {
                var commits = new List<Models.Commit>();
                for (var i = selected.Count - 1; i >= 0; i--)
                {
                    if (selected[i] is Models.Commit c)
                        commits.Add(c);
                }

                if (selected.Count > 0)
                {
                    var menu = histories.CreateContextMenuForSelectedCommits(commits, c => dataGrid.SelectedItems.Add(c));
                    menu?.Open(dataGrid);
                }
            }

            e.Handled = true;
        }

        private async void OnCommitListKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
                return;

            if (sender is DataGrid { SelectedItems: { Count: > 0 } selected })
            {
                if (e.Key == Key.C)
                {
                    var builder = new StringBuilder();
                    foreach (var item in selected)
                    {
                        if (item is Models.Commit commit)
                            builder.Append(commit.SHA.AsSpan(0, 10)).Append(" - ").AppendLine(commit.Subject);
                    }

                    await App.CopyTextAsync(builder.ToString());
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.B && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    var repoView = this.FindAncestorOfType<Repository>();
                    if (repoView?.DataContext is not ViewModels.Repository repo || !repo.CanCreatePopup())
                        return;

                    if (selected.Count == 1 && selected[0] is Models.Commit commit)
                    {
                        repo.ShowPopup(new ViewModels.CreateBranch(repo, commit));
                        e.Handled = true;
                    }

                    return;
                }

                if (e.Key == Key.T && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    var repoView = this.FindAncestorOfType<Repository>();
                    if (repoView?.DataContext is not ViewModels.Repository repo || !repo.CanCreatePopup())
                        return;

                    if (selected.Count == 1 && selected[0] is Models.Commit commit)
                    {
                        repo.ShowPopup(new ViewModels.CreateTag(repo, commit));
                        e.Handled = true;
                    }

                    return;
                }
            }
        }

        private void OnCommitListDoubleTapped(object sender, TappedEventArgs e)
        {
            e.Handled = true;

            if (DataContext is ViewModels.Histories histories &&
                CommitListContainer.SelectedItems is { Count: 1 } &&
                sender is DataGrid grid &&
                !Equals(e.Source, grid))
            {
                if (e.Source is CommitRefsPresenter crp)
                {
                    var decorator = crp.DecoratorAt(e.GetPosition(crp));
                    if (histories.CheckoutBranchByDecorator(decorator))
                        return;
                }

                if (e.Source is Control { DataContext: Models.Commit c })
                    histories.CheckoutBranchByCommit(c);
            }
        }

        private double _lastGraphStartY = 0;
        private double _lastGraphClipWidth = 0;
        private double _lastGraphRowHeight = 0;
    }
}
