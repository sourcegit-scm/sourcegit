using System;
using System.Text;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class HistoriesLayout : Grid
    {
        public static readonly StyledProperty<bool> UseHorizontalProperty =
            AvaloniaProperty.Register<HistoriesLayout, bool>(nameof(UseHorizontal), false);

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
        public static readonly StyledProperty<GridLength> AuthorNameColumnWidthProperty =
            AvaloniaProperty.Register<Histories, GridLength>(nameof(AuthorNameColumnWidth), new GridLength(120));

        public GridLength AuthorNameColumnWidth
        {
            get => GetValue(AuthorNameColumnWidthProperty);
            set => SetValue(AuthorNameColumnWidthProperty, value);
        }

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
                if (DataContext is ViewModels.Histories)
                {
                    var list = CommitListContainer;
                    if (list != null && list.SelectedItems.Count == 1)
                        list.ScrollIntoView(list.SelectedIndex);
                }
            }
        }

        private void OnCommitListLayoutUpdated(object _1, EventArgs _2)
        {
            var y = CommitListContainer.Scroll?.Offset.Y ?? 0;
            var authorNameColumnWidth = AuthorNameColumnWidth.Value;
            if (y != _lastScrollY || authorNameColumnWidth != _lastAuthorNameColumnWidth)
            {
                _lastScrollY = y;
                _lastAuthorNameColumnWidth = authorNameColumnWidth;
                CommitGraph.InvalidateVisual();
            }
        }

        private void OnCommitListSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories)
            {
                histories.Select(CommitListContainer.SelectedItems);
            }
            e.Handled = true;
        }

        private void OnCommitListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories && sender is ListBox { SelectedItems: { Count: > 0 } } list)
            {
                var menu = histories.MakeContextMenu(list);
                menu?.Open(list);
            }
            e.Handled = true;
        }

        private void OnCommitListDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories && sender is ListBox { SelectedItems: { Count: 1 } })
            {
                var source = e.Source as Control;
                var item = source.FindAncestorOfType<ListBoxItem>();
                if (item is { DataContext: Models.Commit commit })
                    histories.DoubleTapped(commit);
            }
            e.Handled = true;
        }

        private void OnCommitListKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
                return;

            // These shortcuts are not mentioned in the Shortcut Reference window. Is this expected?
            if (sender is ListBox { SelectedItems: { Count: > 0 } selected })
            {
                // CTRL/COMMAND + C -> Copy selected commit SHA and subject.
                if (e.Key == Key.C)
                {
                    var builder = new StringBuilder();
                    foreach (var item in selected)
                    {
                        if (item is Models.Commit commit)
                            builder.AppendLine($"{commit.SHA.AsSpan(0, 10)} - {commit.Subject}");
                    }

                    App.CopyText(builder.ToString());
                    e.Handled = true;
                    return;
                }

                // CTRL/COMMAND + B -> shows Create Branch pop-up at selected commit.
                if (e.Key == Key.B)
                {
                    var repoView = this.FindAncestorOfType<Repository>();
                    if (repoView == null)
                        return;

                    var repo = repoView.DataContext as ViewModels.Repository;
                    if (repo == null || !repo.CanCreatePopup())
                        return;

                    if (selected.Count == 1 && selected[0] is Models.Commit commit)
                    {
                        repo.ShowPopup(new ViewModels.CreateBranch(repo, commit));
                        e.Handled = true;
                    }
                }
            }
        }

        private double _lastScrollY = 0;
        private double _lastAuthorNameColumnWidth = 0;
    }
}
