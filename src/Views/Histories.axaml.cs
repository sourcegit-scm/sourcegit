using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class HistoriesLayout : Grid
    {
        public static readonly DirectProperty<HistoriesLayout, bool> UseHorizontalProperty =
            AvaloniaProperty.RegisterDirect<HistoriesLayout, bool>(
                nameof(UseHorizontal),
                static o => o.UseHorizontal,
                static (o, v) => o.UseHorizontal = v);

        public bool UseHorizontal
        {
            get => _useHorizontal;
            set => SetAndRaise(UseHorizontalProperty, ref _useHorizontal, value);
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

        private bool _useHorizontal = false;
    }

    public class HistoriesCommitList : DataGrid
    {
        public static readonly DirectProperty<HistoriesCommitList, int> TotalCommitsProperty =
            AvaloniaProperty.RegisterDirect<HistoriesCommitList, int>(
                nameof(TotalCommits),
                static o => o.TotalCommits,
                static (o, v) => o.TotalCommits = v);

        public int TotalCommits
        {
            get => _totalCommits;
            set => SetAndRaise(TotalCommitsProperty, ref _totalCommits, value);
        }

        public static readonly DirectProperty<HistoriesCommitList, List<Models.Commit>> SelectedCommitsProperty =
            AvaloniaProperty.RegisterDirect<HistoriesCommitList, List<Models.Commit>>(
                nameof(SelectedCommits),
                static o => o.SelectedCommits,
                static (o, v) => o.SelectedCommits = v);

        public List<Models.Commit> SelectedCommits
        {
            get => _selectedCommits;
            set => SetAndRaise(SelectedCommitsProperty, ref _selectedCommits, value);
        }

        protected override Type StyleKeyOverride => typeof(DataGrid);

        public HistoriesCommitList()
        {
            SelectionMode = DataGridSelectionMode.Extended;
            CanUserReorderColumns = false;
            CanUserResizeColumns = false;
            CanUserSortColumns = false;
            AutoGenerateColumns = false;
            IsReadOnly = true;
            HeadersVisibility = DataGridHeadersVisibility.Column;
            ClipboardCopyMode = DataGridClipboardCopyMode.None;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            ApplySelection();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedCommitsProperty && IsLoaded && !_ignoreSelectionChanged)
            {
                if (change.OldValue is List<Models.Commit> { Count: 1 } old &&
                    change.NewValue is List<Models.Commit> { Count: 1 } cur &&
                    old[0] == cur[0])
                    ScrollIntoView(old[0], null);
                else
                    ApplySelection();
            }
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            if (ItemsSource is not IList<Models.Commit> items)
                return;

            var commits = new List<Models.Commit>();
            foreach (var o in SelectedItems)
            {
                if (o is Models.Commit c)
                    commits.Add(c);
            }

            if (e.AddedItems.Count == 1)
            {
                ScrollIntoView(e.AddedItems[0], null);
            }
            else if (e.AddedItems.Count > 1 && e.AddedItems[0] is Models.Commit first)
            {
                var firstIndex = items.IndexOf(first);
                if (firstIndex > 0)
                {
                    var prev = items[firstIndex - 1];
                    if (commits.Contains(prev))
                        ScrollIntoView(e.AddedItems[^1], null);
                    else
                        ScrollIntoView(first, null);
                }
            }

            if (!_ignoreSelectionChanged)
            {
                _ignoreSelectionChanged = true;

                var old = SelectedCommits;
                if (old.Count != commits.Count)
                {
                    SelectedCommits = commits;
                }
                else if (commits.Count > 0)
                {
                    var set = new HashSet<string>();
                    foreach (var c in old)
                        set.Add(c.SHA);

                    var equals = true;
                    foreach (var c in commits)
                    {
                        if (!set.Contains(c.SHA))
                        {
                            equals = false;
                            break;
                        }
                    }

                    if (!equals)
                        SelectedCommits = commits;
                }

                _ignoreSelectionChanged = false;
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Alt)
            {
                if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    if (this.FindAncestorOfType<Histories>() is { } histories)
                        await histories.GotoChild();
                }
                else if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    if (this.FindAncestorOfType<Histories>() is { } histories)
                        await histories.GotoParent();
                }
            }
            else if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) &&
                SelectedItems is { Count: > 0 } selected &&
                e.Key == Key.C)
            {
                var builder = new StringBuilder();
                foreach (var item in selected)
                {
                    if (item is Models.Commit commit)
                        builder.Append(commit.SHA.AsSpan(0, 10)).Append(" - ").AppendLine(commit.Subject);
                }

                e.Handled = true;
                await this.CopyTextAsync(builder.ToString());
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }

        private void ApplySelection()
        {
            _ignoreSelectionChanged = true;

            if (SelectedCommits == null || SelectedCommits.Count == 0)
            {
                SelectedItems.Clear();
            }
            else if (SelectedCommits.Count == TotalCommits)
            {
                SelectAll();
            }
            else
            {
                IncrNoSelectionChangeCount();
                SelectedItems.Clear();
                foreach (var c in SelectedCommits)
                    SelectedItems.Add(c);
                DecrNoSelectionChangeCount();
            }

            _ignoreSelectionChanged = false;
        }

        private void IncrNoSelectionChangeCount()
        {
            var property = typeof(DataGrid).GetProperty("NoSelectionChangeCount", BindingFlags.Instance | BindingFlags.NonPublic);
            if (property != null)
            {
                var old = (int)property.GetValue(this)!;
                property.SetValue(this, old + 1);
            }
        }

        private void DecrNoSelectionChangeCount()
        {
            var property = typeof(DataGrid).GetProperty("NoSelectionChangeCount", BindingFlags.Instance | BindingFlags.NonPublic);
            if (property != null)
            {
                var old = (int)property.GetValue(this)!;
                property.SetValue(this, old - 1);
            }
        }

        private bool _ignoreSelectionChanged = false;
        private int _totalCommits = 0;
        private List<Models.Commit> _selectedCommits = [];
    }

    public partial class Histories : UserControl
    {
        public static readonly DirectProperty<Histories, Models.Branch> CurrentBranchProperty =
            AvaloniaProperty.RegisterDirect<Histories, Models.Branch>(
                nameof(CurrentBranch),
                static o => o.CurrentBranch,
                static (o, v) => o.CurrentBranch = v);

        public Models.Branch CurrentBranch
        {
            get => _currentBranch;
            set => SetAndRaise(CurrentBranchProperty, ref _currentBranch, value);
        }

        public static readonly DirectProperty<Histories, Models.Bisect> BisectProperty =
            AvaloniaProperty.RegisterDirect<Histories, Models.Bisect>(
                nameof(Bisect),
                static o => o.Bisect,
                static (o, v) => o.Bisect = v);

        public Models.Bisect Bisect
        {
            get => _bisect;
            set => SetAndRaise(BisectProperty, ref _bisect, value);
        }

        public static readonly DirectProperty<Histories, AvaloniaList<Models.IssueTracker>> IssueTrackersProperty =
            AvaloniaProperty.RegisterDirect<Histories, AvaloniaList<Models.IssueTracker>>(
                nameof(IssueTrackers),
                static o => o.IssueTrackers,
                static (o, v) => o.IssueTrackers = v);

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => _issueTrackers;
            set => SetAndRaise(IssueTrackersProperty, ref _issueTrackers, value);
        }

        public static readonly DirectProperty<Histories, bool> IsScrollToTopVisibleProperty =
            AvaloniaProperty.RegisterDirect<Histories, bool>(
                nameof(IsScrollToTopVisible),
                static o => o.IsScrollToTopVisible,
                static (o, v) => o.IsScrollToTopVisible = v);

        public bool IsScrollToTopVisible
        {
            get => _isScrollToTopVisible;
            set => SetAndRaise(IsScrollToTopVisibleProperty, ref _isScrollToTopVisible, value);
        }

        public static readonly DirectProperty<Histories, bool> IsDetailsPanelExpandedProperty =
            AvaloniaProperty.RegisterDirect<Histories, bool>(
                nameof(IsDetailsPanelExpanded),
                static o => o.IsDetailsPanelExpanded,
                static (o, v) => o.IsDetailsPanelExpanded = v);

        public bool IsDetailsPanelExpanded
        {
            get => _isDetailsPanelExpanded;
            set => SetAndRaise(IsDetailsPanelExpandedProperty, ref _isDetailsPanelExpanded, value);
        }

        public Histories()
        {
            InitializeComponent();
        }

        public async Task GotoParent()
        {
            if (DataContext is not ViewModels.Histories vm)
                return;

            if (!CommitListContainer.IsKeyboardFocusWithin)
                return;

            if (CommitListContainer.SelectedItems is not { Count: 1 } selected)
                return;

            if (selected[0] is not Models.Commit { Parents.Count: > 0 } commit)
                return;

            if (commit.Parents.Count == 1)
            {
                vm.NavigateTo(commit.Parents[0]);
                return;
            }

            var parents = new List<Models.Commit>();
            foreach (var sha in commit.Parents)
            {
                var c = await vm.GetCommitAsync(sha);
                if (c != null)
                    parents.Add(c);
            }

            if (parents.Count == 1)
            {
                vm.NavigateTo(parents[0].SHA);
            }
            else if (parents.Count > 1 && TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new GotoRevisionSelector();
                dialog.RevisionList.ItemsSource = parents;

                var c = await dialog.ShowDialog<Models.Commit>(owner);
                if (c != null)
                    vm.NavigateTo(c.SHA);
            }
        }

        public async Task GotoChild()
        {
            if (DataContext is not ViewModels.Histories vm)
                return;

            if (!CommitListContainer.IsKeyboardFocusWithin)
                return;

            if (CommitListContainer.SelectedItems is not { Count: 1 } selected)
                return;

            if (selected[0] is not Models.Commit { Parents.Count: > 0 } commit)
                return;

            var children = new List<Models.Commit>();
            var sha = commit.SHA;
            foreach (var c in vm.Commits)
            {
                foreach (var p in c.Parents)
                {
                    if (sha.StartsWith(p, StringComparison.Ordinal))
                        children.Add(c);
                }

                if (sha.Equals(c.SHA, StringComparison.Ordinal))
                    break;
            }

            if (children.Count == 1)
            {
                vm.NavigateTo(children[0].SHA);
            }
            else if (children.Count > 1 && TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new GotoRevisionSelector();
                dialog.RevisionList.ItemsSource = children;

                var c = await dialog.ShowDialog<Models.Commit>(owner);
                if (c != null)
                    vm.NavigateTo(c.SHA);
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.Histories vm)
                CommitListContainer.Columns[1].Width = new(vm.AuthorColumnWidth, DataGridLengthUnitType.Pixel);
        }

        private void OnCommitListHeaderPointerMoved(object sender, PointerEventArgs e)
        {
            if (sender is not Border border)
                return;

            if (DataContext is not ViewModels.Histories { IsAuthorColumnVisible: true } vm)
                return;

            var pos = e.GetPosition(border);
            if (_resizingAuthorColumn)
            {
                var posX = CommitListContainer.Columns[0].ActualWidth;
                var maxW = posX + CommitListContainer.Columns[1].ActualWidth - 100;
                var delta = posX - pos.X;
                var w = Math.Max(Math.Min(vm.AuthorColumnWidth + delta, maxW), 80);
                CommitListContainer.Columns[1].Width = new(w, DataGridLengthUnitType.Pixel);
                vm.AuthorColumnWidth = w;
            }
            else
            {
                var dis = CommitListContainer.Columns[0].ActualWidth - 4 - pos.X;
                if (dis < 4 && dis > -4)
                {
                    if (border.Cursor != _resizingCursor)
                        border.Cursor = _resizingCursor;
                }
                else if (border.Cursor != Cursor.Default)
                {
                    border.Cursor = Cursor.Default;
                }
            }
        }

        private void OnCommitListHeaderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not Border border)
                return;

            var pos = e.GetPosition(border);
            var dis = CommitListContainer.Columns[0].ActualWidth - 4 - pos.X;
            if (dis > 4 || dis < -4)
                return;

            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                _resizingAuthorColumn = true;
                e.Handled = true;
            }
        }

        private void OnCommitListHeaderPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _resizingAuthorColumn = false;
        }

        private void OnCommitListHeaderContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is not ViewModels.Histories vm)
                return;

            if (sender is not Border border)
                return;

            var columnsHeader = new MenuItem();
            columnsHeader.Header = new TextBlock() { Text = App.Text("Histories.ShowColumns"), FontWeight = FontWeight.Bold };
            columnsHeader.IsEnabled = false;

            var authorColumn = new MenuItem();
            authorColumn.Header = App.Text("Histories.Header.Author");
            if (vm.IsAuthorColumnVisible)
                authorColumn.Icon = this.CreateMenuIcon("Icons.Check");
            authorColumn.Click += (_, ev) =>
            {
                vm.IsAuthorColumnVisible = !vm.IsAuthorColumnVisible;
                ev.Handled = true;
            };

            var shaColumn = new MenuItem();
            shaColumn.Header = App.Text("Histories.Header.SHA");
            if (vm.IsSHAColumnVisible)
                shaColumn.Icon = this.CreateMenuIcon("Icons.Check");
            shaColumn.Click += (_, ev) =>
            {
                vm.IsSHAColumnVisible = !vm.IsSHAColumnVisible;
                ev.Handled = true;
            };

            var authorTimeColumn = new MenuItem();
            authorTimeColumn.Header = App.Text("Histories.Header.AuthorTime");
            if (vm.IsAuthorTimeColumnVisible)
                authorTimeColumn.Icon = this.CreateMenuIcon("Icons.Check");
            authorTimeColumn.Click += (_, ev) =>
            {
                vm.IsAuthorTimeColumnVisible = !vm.IsAuthorTimeColumnVisible;
                ev.Handled = true;
            };

            var commitTimeColumn = new MenuItem();
            commitTimeColumn.Header = App.Text("Histories.Header.CommitTime");
            if (vm.IsCommitTimeColumnVisible)
                commitTimeColumn.Icon = this.CreateMenuIcon("Icons.Check");
            commitTimeColumn.Click += (_, ev) =>
            {
                vm.IsCommitTimeColumnVisible = !vm.IsCommitTimeColumnVisible;
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(columnsHeader);
            menu.Items.Add(authorColumn);
            menu.Items.Add(shaColumn);
            menu.Items.Add(authorTimeColumn);
            menu.Items.Add(commitTimeColumn);
            menu.Open(border);
            e.Handled = true;
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

            IsScrollToTopVisible = startY >= rowHeight;

            var clipWidth = dataGrid.Columns[0].ActualWidth - 4;
            var lastLayout = CommitGraph.Layout;
            if (lastLayout == null ||
                Math.Abs(lastLayout.StartY - startY) > 0.01 ||
                Math.Abs(lastLayout.ClipWidth - clipWidth) > 0.01 ||
                Math.Abs(lastLayout.RowHeight - rowHeight) > 0.01)
                CommitGraph.Layout = new(startY, clipWidth, rowHeight);
        }

        private void OnScrollToTopPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories)
                CommitListContainer.ScrollIntoView(histories.Commits[0], null);
        }

        private void OnCommitListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is not { DataContext: ViewModels.Repository repo })
                return;

            var selected = CommitListContainer.SelectedItems;
            if (selected is not { Count: > 0 })
                return;

            var commits = new List<Models.Commit>();
            for (var i = selected.Count - 1; i >= 0; i--)
            {
                if (selected[i] is Models.Commit c)
                    commits.Add(c);
            }

            if (selected.Count > 1)
            {
                var menu = CreateContextMenuForMultipleCommits(repo, commits);
                menu.Open(CommitListContainer);
            }
            else if (selected.Count == 1)
            {
                var menu = CreateContextMenuForSingleCommit(repo, commits[0]);
                menu.Open(CommitListContainer);
            }

            e.Handled = true;
        }

        private async void OnCommitListDoubleTapped(object sender, TappedEventArgs e)
        {
            e.Handled = true;

            if (DataContext is ViewModels.Histories histories &&
                CommitListContainer.SelectedItems is { Count: 1 } &&
                e.Source is Control { DataContext: Models.Commit c })
            {
                if (histories.Bisect != null)
                {
                    histories.CheckoutCommitDetached(c);
                    return;
                }

                if (e.Source is CommitRefsPresenter crp)
                {
                    var decorator = crp.DecoratorAt(e.GetPosition(crp));
                    var succ = await histories.CheckoutBranchByDecoratorAsync(decorator);
                    if (succ)
                        return;
                }

                await histories.CheckoutBranchByCommitAsync(c);
            }
        }

        private void OnCommitGraphLoaded(object sender, RoutedEventArgs e)
        {
            // Force-update the graph layout to ensure the graph is correctly rendered when it's loaded.
            OnCommitListLayoutUpdated(sender, e);
        }

        private void OnTabHeaderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (ViewModels.Preferences.Instance.UseTwoColumnsLayoutInHistories)
                return;

            if (DataContext is not ViewModels.Histories vm)
                return;

            if (vm.IsCollapseDetails)
                vm.IsCollapseDetails = false;
        }

        private void OnOpenDetailsAsStandalone(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Histories vm)
            {
                if (vm.DetailContext is ViewModels.CommitDetail detail)
                {
                    var standalone = new CommitDetailStandalone();
                    standalone.DataContext = detail.Clone();
                    this.ShowWindow(standalone);
                }
                else if (vm.DetailContext is ViewModels.RevisionCompare compare)
                {
                    var standalone = new RevisionCompareStandalone();
                    standalone.DataContext = compare.Clone();
                    this.ShowWindow(standalone);
                }
            }

            e.Handled = true;
        }

        private ContextMenu CreateContextMenuForMultipleCommits(ViewModels.Repository repo, List<Models.Commit> selected)
        {
            var canCherryPick = true;
            var canMerge = true;

            foreach (var c in selected)
            {
                if (c.IsMerged)
                {
                    canMerge = false;
                    canCherryPick = false;
                }
                else if (c.Parents.Count > 1)
                {
                    canCherryPick = false;
                }
            }

            var menu = new ContextMenu();

            if (!repo.IsBare)
            {
                if (canCherryPick)
                {
                    var cherryPick = new MenuItem();
                    cherryPick.Header = App.Text("CommitCM.CherryPickMultiple");
                    cherryPick.Icon = this.CreateMenuIcon("Icons.CherryPick");
                    cherryPick.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.CherryPick(repo, selected));
                        e.Handled = true;
                    };
                    menu.Items.Add(cherryPick);
                }

                if (canMerge)
                {
                    var merge = new MenuItem();
                    merge.Header = App.Text("CommitCM.MergeMultiple");
                    merge.Icon = this.CreateMenuIcon("Icons.Merge");
                    merge.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.MergeMultiple(repo, selected));
                        e.Handled = true;
                    };
                    menu.Items.Add(merge);
                }

                if (canCherryPick || canMerge)
                    menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var saveToPatch = new MenuItem();
            saveToPatch.Icon = this.CreateMenuIcon("Icons.Save");
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                try
                {
                    var picker = await storageProvider.OpenFolderPickerAsync(options);
                    if (picker.Count == 1)
                    {
                        var folder = picker[0];
                        var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                        var succ = false;
                        for (var i = 0; i < selected.Count; i++)
                        {
                            succ = await repo.SaveCommitAsPatchAsync(selected[i], folderPath, i);
                            if (!succ)
                                break;
                        }

                        if (succ)
                            repo.SendNotification(App.Text("SaveAsPatchSuccess"));
                    }
                }
                catch (Exception exception)
                {
                    repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copyInfos = new MenuItem();
            copyInfos.Header = App.Text("CommitCM.CopySHA") + " - " + App.Text("CommitCM.CopySubject");
            copyInfos.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyInfos.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.Append(c.SHA.AsSpan(0, 10)).Append(" - ").AppendLine(c.Subject);

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copyShas = new MenuItem();
            copyShas.Header = App.Text("CommitCM.CopySHA");
            copyShas.Icon = this.CreateMenuIcon("Icons.Hash");
            copyShas.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.AppendLine(c.SHA);

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copySubjects = new MenuItem();
            copySubjects.Header = App.Text("CommitCM.CopySubject");
            copySubjects.Icon = this.CreateMenuIcon("Icons.Subject");
            copySubjects.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.AppendLine(c.Subject);

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copyMessage = new MenuItem();
            copyMessage.Header = App.Text("CommitCM.CopyCommitMessage");
            copyMessage.Icon = this.CreateMenuIcon("Icons.Message");
            copyMessage.Click += async (_, e) =>
            {
                var vm = DataContext as ViewModels.Histories;
                var messages = new List<string>();
                foreach (var c in selected)
                {
                    var message = await vm!.GetCommitFullMessageAsync(c);
                    messages.Add(message);
                }

                await this.CopyTextAsync(string.Join("\n-----\n", messages));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Items.Add(copyInfos);
            copy.Items.Add(new MenuItem() { Header = "-" });
            copy.Items.Add(copyShas);
            copy.Items.Add(copySubjects);
            copy.Items.Add(copyMessage);
            menu.Items.Add(copy);
            return menu;
        }

        private ContextMenu CreateContextMenuForSingleCommit(ViewModels.Repository repo, Models.Commit commit)
        {
            var current = repo.CurrentBranch;
            var vm = DataContext as ViewModels.Histories;
            if (current == null || vm == null)
                return null;

            var menu = new ContextMenu();
            var tags = new List<Models.Tag>();
            var isHead = commit.IsCurrentHead;

            if (commit.HasDecorators)
            {
                foreach (var d in commit.Decorators)
                {
                    switch (d.Type)
                    {
                        case Models.DecoratorType.CurrentBranchHead:
                            FillCurrentBranchMenu(menu, repo, current);
                            break;
                        case Models.DecoratorType.LocalBranchHead:
                            var lb = repo.Branches.Find(x => x.IsLocal && d.Name.Equals(x.Name, StringComparison.Ordinal));
                            FillOtherLocalBranchMenu(menu, repo, lb, current, commit.IsMerged);
                            break;
                        case Models.DecoratorType.RemoteBranchHead:
                            var rb = repo.Branches.Find(x => !x.IsLocal && d.Name.Equals(x.FriendlyName, StringComparison.Ordinal));
                            FillRemoteBranchMenu(menu, repo, rb, current, commit.IsMerged);
                            break;
                        case Models.DecoratorType.Tag:
                            var t = repo.Tags.Find(x => d.Name.Equals(x.Name, StringComparison.Ordinal));
                            if (t != null)
                                tags.Add(t);
                            break;
                    }
                }

                if (menu.Items.Count > 0)
                    menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (tags.Count > 0)
            {
                foreach (var tag in tags)
                    FillTagMenu(menu, repo, tag, current);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var createBranch = new MenuItem();
            createBranch.Icon = this.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+B" : "Ctrl+Shift+B";
            createBranch.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.CreateBranch(repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var createTag = new MenuItem();
            createTag.Icon = this.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+T" : "Ctrl+Shift+T";
            createTag.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.CreateTag(repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var target = commit.GetFriendlyName();
                if (target.Length > 40)
                    target = commit.SHA.Substring(0, 10);

                if (!isHead)
                {
                    var reset = new MenuItem();
                    reset.Header = App.Text("CommitCM.Reset", current.Name, target);
                    reset.Icon = this.CreateMenuIcon("Icons.Reset");
                    reset.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.Reset(repo, current, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(reset);
                }

                if (!commit.IsMerged)
                {
                    var rebase = new MenuItem();
                    rebase.Header = App.Text("CommitCM.Rebase", current.Name, target);
                    rebase.Icon = this.CreateMenuIcon("Icons.Rebase");
                    rebase.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.Rebase(repo, current, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(rebase);

                    var merge = new MenuItem();
                    merge.Header = App.Text("BranchCM.Merge", target, current.Name);
                    merge.Icon = this.CreateMenuIcon("Icons.Merge");
                    merge.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                        {
                            var found = false;
                            foreach (var d in commit.Decorators)
                            {
                                if (d.Type == Models.DecoratorType.LocalBranchHead)
                                {
                                    var b = repo.Branches.Find(x => x.IsLocal && x.Name.Equals(d.Name, StringComparison.Ordinal));
                                    if (b != null)
                                    {
                                        found = true;
                                        repo.ShowPopup(new ViewModels.Merge(repo, b, current.Name, false));
                                        break;
                                    }
                                }
                                else if (d.Type == Models.DecoratorType.RemoteBranchHead)
                                {
                                    var rb = repo.Branches.Find(x => !x.IsLocal && x.FriendlyName.Equals(d.Name, StringComparison.Ordinal));
                                    if (rb != null)
                                    {
                                        found = true;
                                        repo.ShowPopup(new ViewModels.Merge(repo, rb, current.Name, false));
                                        break;
                                    }
                                }
                                else if (d.Type == Models.DecoratorType.Tag)
                                {
                                    var t = repo.Tags.Find(x => x.Name.Equals(d.Name, StringComparison.Ordinal));
                                    if (t != null)
                                    {
                                        found = true;
                                        repo.ShowPopup(new ViewModels.Merge(repo, t, current.Name));
                                        break;
                                    }
                                }
                            }

                            if (!found)
                                repo.ShowPopup(new ViewModels.Merge(repo, commit, current.Name));
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(merge);

                    var cherryPick = new MenuItem();
                    cherryPick.Header = App.Text("CommitCM.CherryPick");
                    cherryPick.Icon = this.CreateMenuIcon("Icons.CherryPick");
                    cherryPick.Click += async (_, e) =>
                    {
                        await vm.CherryPickAsync(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(cherryPick);
                }

                var revert = new MenuItem();
                revert.Header = App.Text("CommitCM.Revert");
                revert.Icon = this.CreateMenuIcon("Icons.Undo");
                revert.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Revert(repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(revert);

                if (!isHead)
                {
                    var checkoutCommit = new MenuItem();
                    checkoutCommit.Header = App.Text("CommitCM.Checkout");
                    checkoutCommit.Icon = this.CreateMenuIcon("Icons.Detached");
                    checkoutCommit.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.CheckoutDetached(repo, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(checkoutCommit);
                }

                if (commit.IsMerged && commit.Parents.Count > 0)
                {
                    var interactiveRebase = new MenuItem();
                    interactiveRebase.Header = App.Text("CommitCM.InteractiveRebase");
                    interactiveRebase.Icon = this.CreateMenuIcon("Icons.InteractiveRebase");

                    if (!isHead)
                    {
                        var manually = new MenuItem();
                        manually.Header = App.Text("CommitCM.InteractiveRebase.Manually", current.Name, target);
                        manually.Click += async (_, e) =>
                        {
                            await this.ShowDialogAsync(new ViewModels.InteractiveRebase(repo, commit));
                            e.Handled = true;
                        };

                        interactiveRebase.Items.Add(manually);
                        interactiveRebase.Items.Add(new MenuItem() { Header = "-" });
                    }

                    var reword = new MenuItem();
                    reword.Header = App.Text("CommitCM.InteractiveRebase.Reword");
                    reword.Icon = this.CreateMenuIcon("Icons.Rename");
                    reword.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Reword);
                        e.Handled = true;
                    };

                    var edit = new MenuItem();
                    edit.Header = App.Text("CommitCM.InteractiveRebase.Edit");
                    edit.Icon = this.CreateMenuIcon("Icons.Edit");
                    edit.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Edit);
                        e.Handled = true;
                    };

                    var squash = new MenuItem();
                    squash.Header = App.Text("CommitCM.InteractiveRebase.Squash");
                    squash.Icon = this.CreateMenuIcon("Icons.SquashIntoParent");
                    squash.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Squash);
                        e.Handled = true;
                    };

                    var fixup = new MenuItem();
                    fixup.Header = App.Text("CommitCM.InteractiveRebase.Fixup");
                    fixup.Icon = this.CreateMenuIcon("Icons.Fix");
                    fixup.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Fixup);
                        e.Handled = true;
                    };

                    var drop = new MenuItem();
                    drop.Header = App.Text("CommitCM.InteractiveRebase.Drop");
                    drop.Icon = this.CreateMenuIcon("Icons.Clear");
                    drop.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Drop);
                        e.Handled = true;
                    };

                    interactiveRebase.Items.Add(reword);
                    interactiveRebase.Items.Add(edit);
                    interactiveRebase.Items.Add(squash);
                    interactiveRebase.Items.Add(fixup);
                    interactiveRebase.Items.Add(drop);

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(interactiveRebase);
                }
                else
                {
                    var interactiveRebase = new MenuItem();
                    interactiveRebase.Header = App.Text("CommitCM.InteractiveRebase.Manually", current.Name, target);
                    interactiveRebase.Icon = this.CreateMenuIcon("Icons.InteractiveRebase");
                    interactiveRebase.Click += async (_, e) =>
                    {
                        await this.ShowDialogAsync(new ViewModels.InteractiveRebase(repo, commit));
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(interactiveRebase);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (!isHead)
            {
                if (current.Ahead.Contains(commit.SHA))
                {
                    var upstream = repo.Branches.Find(x => x.FullName.Equals(current.Upstream, StringComparison.Ordinal));
                    var pushRevision = new MenuItem();
                    pushRevision.Header = App.Text("CommitCM.PushRevision", commit.SHA.Substring(0, 10), upstream.FriendlyName);
                    pushRevision.Icon = this.CreateMenuIcon("Icons.Push");
                    pushRevision.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.PushRevision(repo, commit, upstream));
                        e.Handled = true;
                    };
                    menu.Items.Add(pushRevision);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var compareWithHead = new MenuItem();
                compareWithHead.Header = App.Text("CommitCM.CompareWithHead");
                compareWithHead.Icon = this.CreateMenuIcon("Icons.Compare");
                compareWithHead.Click += async (_, e) =>
                {
                    var head = await vm.CompareWithHeadAsync(commit);
                    if (head != null)
                        CommitListContainer.SelectedItems.Add(head);

                    e.Handled = true;
                };
                menu.Items.Add(compareWithHead);

                if (repo.LocalChangesCount > 0)
                {
                    var compareWithWorktree = new MenuItem();
                    compareWithWorktree.Header = App.Text("CommitCM.CompareWithWorktree");
                    compareWithWorktree.Icon = this.CreateMenuIcon("Icons.Compare");
                    compareWithWorktree.Click += (_, e) =>
                    {
                        vm.CompareWithWorktree(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(compareWithWorktree);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var saveToPatch = new MenuItem();
            saveToPatch.Icon = this.CreateMenuIcon("Icons.Save");
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                try
                {
                    var selected = await storageProvider.OpenFolderPickerAsync(options);
                    if (selected.Count == 1)
                    {
                        var folder = selected[0];
                        var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                        var succ = await repo.SaveCommitAsPatchAsync(commit, folderPath);
                        if (succ)
                            repo.SendNotification(App.Text("SaveAsPatchSuccess"));
                    }
                }
                catch (Exception exception)
                {
                    repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);

            var archive = new MenuItem();
            archive.Icon = this.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.Archive(repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var actions = repo.GetCustomActions(Models.CustomActionScope.Commit);
            if (actions.Count > 0)
            {
                var custom = new MenuItem();
                custom.Header = App.Text("CommitCM.CustomAction");
                custom.Icon = this.CreateMenuIcon("Icons.Action");

                foreach (var action in actions)
                {
                    var (dup, label) = action;
                    var item = new MenuItem();
                    item.Icon = this.CreateMenuIcon("Icons.Action");
                    item.Header = label;
                    item.Click += async (_, e) =>
                    {
                        await repo.ExecCustomActionAsync(dup, commit);
                        e.Handled = true;
                    };

                    custom.Items.Add(item);
                }

                menu.Items.Add(custom);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var copyInfo = new MenuItem();
            copyInfo.Header = App.Text("CommitCM.CopySHA") + " - " + App.Text("CommitCM.CopySubject");
            copyInfo.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyInfo.Click += async (_, e) =>
            {
                await this.CopyTextAsync($"{commit.SHA.AsSpan(0, 10)} - {commit.Subject}");
                e.Handled = true;
            };

            var copySHA = new MenuItem();
            copySHA.Header = App.Text("CommitCM.CopySHA");
            copySHA.Icon = this.CreateMenuIcon("Icons.Hash");
            copySHA.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.SHA);
                e.Handled = true;
            };

            var copySubject = new MenuItem();
            copySubject.Header = App.Text("CommitCM.CopySubject");
            copySubject.Icon = this.CreateMenuIcon("Icons.Subject");
            copySubject.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.Subject);
                e.Handled = true;
            };

            var copyMessage = new MenuItem();
            copyMessage.Header = App.Text("CommitCM.CopyCommitMessage");
            copyMessage.Icon = this.CreateMenuIcon("Icons.Message");
            copyMessage.Click += async (_, e) =>
            {
                var message = await vm.GetCommitFullMessageAsync(commit);
                await this.CopyTextAsync(message);
                e.Handled = true;
            };

            var copyAuthor = new MenuItem();
            copyAuthor.Header = App.Text("CommitCM.CopyAuthor");
            copyAuthor.Icon = this.CreateMenuIcon("Icons.User");
            copyAuthor.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.Author.ToString());
                e.Handled = true;
            };

            var copyCommitter = new MenuItem();
            copyCommitter.Header = App.Text("CommitCM.CopyCommitter");
            copyCommitter.Icon = this.CreateMenuIcon("Icons.User");
            copyCommitter.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.Committer.ToString());
                e.Handled = true;
            };

            var copyAuthorTime = new MenuItem();
            copyAuthorTime.Header = App.Text("CommitCM.CopyAuthorTime");
            copyAuthorTime.Icon = this.CreateMenuIcon("Icons.DateTime");
            copyAuthorTime.Click += async (_, e) =>
            {
                await this.CopyTextAsync(Models.DateTimeFormat.Format(commit.AuthorTime));
                e.Handled = true;
            };

            var copyCommitterTime = new MenuItem();
            copyCommitterTime.Header = App.Text("CommitCM.CopyCommitterTime");
            copyCommitterTime.Icon = this.CreateMenuIcon("Icons.DateTime");
            copyCommitterTime.Click += async (_, e) =>
            {
                await this.CopyTextAsync(Models.DateTimeFormat.Format(commit.CommitterTime));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Items.Add(copyInfo);
            copy.Items.Add(new MenuItem() { Header = "-" });
            copy.Items.Add(copySHA);
            copy.Items.Add(copySubject);
            copy.Items.Add(copyMessage);
            copy.Items.Add(copyAuthor);
            copy.Items.Add(copyCommitter);
            copy.Items.Add(copyAuthorTime);
            copy.Items.Add(copyCommitterTime);
            menu.Items.Add(copy);

            return menu;
        }

        private void FillCurrentBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Icon = this.CreateMenuIcon("Icons.Branch");
            submenu.Header = current.Name;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, current);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!string.IsNullOrEmpty(current.Upstream))
            {
                var upstream = current.Upstream.Substring(13);

                var fastForward = new MenuItem();
                fastForward.Header = App.Text("BranchCM.FastForward", upstream);
                fastForward.Icon = this.CreateMenuIcon("Icons.FastForward");
                fastForward.IsEnabled = current.Ahead.Count == 0 && current.Behind.Count > 0;
                fastForward.Click += async (_, e) =>
                {
                    var b = repo.Branches.Find(x => x.FriendlyName == upstream);
                    if (b == null)
                        return;

                    if (repo.CanCreatePopup())
                        await repo.ShowAndStartPopupAsync(new ViewModels.Merge(repo, b, current.Name, true));

                    e.Handled = true;
                };
                submenu.Items.Add(fastForward);

                var pull = new MenuItem();
                pull.Header = App.Text("BranchCM.Pull", upstream);
                pull.Icon = this.CreateMenuIcon("Icons.Pull");
                pull.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Pull(repo, null));
                    e.Handled = true;
                };
                submenu.Items.Add(pull);
            }

            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", current.Name);
            push.Icon = this.CreateMenuIcon("Icons.Push");
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.Push(repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", current.Name);
            rename.Icon = this.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.RenameBranch(repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(rename);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var type = repo.GetGitFlowType(current);
                if (type != Models.GitFlowBranchType.None)
                {
                    var finish = new MenuItem();
                    finish.Header = App.Text("BranchCM.Finish", current.Name);
                    finish.Icon = this.CreateMenuIcon("Icons.GitFlow.Finish");
                    finish.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowFinish(repo, current, type));
                        e.Handled = true;
                    };
                    submenu.Items.Add(finish);
                    submenu.Items.Add(new MenuItem() { Header = "-" });
                }
            }

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(current.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillOtherLocalBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch branch, Models.Branch current, bool merged)
        {
            var submenu = new MenuItem();
            submenu.Icon = this.CreateMenuIcon("Icons.Branch");
            submenu.Header = branch.Name;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, branch);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var checkout = new MenuItem();
                checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
                checkout.Icon = this.CreateMenuIcon("Icons.Check");
                checkout.Click += async (_, e) =>
                {
                    await repo.CheckoutBranchAsync(branch);
                    e.Handled = true;
                };
                submenu.Items.Add(checkout);

                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", branch.Name, current.Name);
                merge.Icon = this.CreateMenuIcon("Icons.Merge");
                merge.IsEnabled = !merged;
                merge.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Merge(repo, branch, current.Name, false));
                    e.Handled = true;
                };
                submenu.Items.Add(merge);
            }

            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", branch.Name);
            push.Icon = this.CreateMenuIcon("Icons.Push");
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.Push(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Icon = this.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.RenameBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.Icon = this.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var type = repo.GetGitFlowType(branch);
                if (type != Models.GitFlowBranchType.None)
                {
                    var finish = new MenuItem();
                    finish.Header = App.Text("BranchCM.Finish", branch.Name);
                    finish.Icon = this.CreateMenuIcon("Icons.GitFlow.Finish");
                    finish.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowFinish(repo, branch, type));
                        e.Handled = true;
                    };
                    submenu.Items.Add(finish);
                    submenu.Items.Add(new MenuItem() { Header = "-" });
                }
            }

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareWithSpecial", current.Name);
            compare.Icon = this.CreateMenuIcon("Icons.Compare");
            compare.Click += (_, e) =>
            {
                this.ShowWindow(new ViewModels.Compare(repo, current, branch));
                e.Handled = true;
            };

            submenu.Items.Add(compare);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(branch.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillRemoteBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch branch, Models.Branch current, bool merged)
        {
            if (branch == null)
                return;

            var name = branch.FriendlyName;

            var submenu = new MenuItem();
            submenu.Icon = this.CreateMenuIcon("Icons.Branch");
            submenu.Header = name;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, branch);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", name);
            checkout.Icon = this.CreateMenuIcon("Icons.Check");
            checkout.Click += async (_, e) =>
            {
                await repo.CheckoutBranchAsync(branch);
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = App.Text("BranchCM.Merge", name, current.Name);
            merge.Icon = this.CreateMenuIcon("Icons.Merge");
            merge.IsEnabled = !merged;
            merge.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.Merge(repo, branch, current.Name, false));
                e.Handled = true;
            };
            submenu.Items.Add(merge);

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", name);
            delete.Icon = this.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareWithSpecial", current.Name);
            compare.Icon = this.CreateMenuIcon("Icons.Compare");
            compare.Click += (_, e) =>
            {
                this.ShowWindow(new ViewModels.Compare(repo, current, branch));
                e.Handled = true;
            };

            submenu.Items.Add(compare);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillTagMenu(ContextMenu menu, ViewModels.Repository repo, Models.Tag tag, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Header = tag.Name;
            submenu.Icon = this.CreateMenuIcon("Icons.Tag");
            submenu.MinWidth = 200;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, tag);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var push = new MenuItem();
            push.Header = App.Text("TagCM.Push", tag.Name);
            push.Icon = this.CreateMenuIcon("Icons.Push");
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.PushTag(repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var delete = new MenuItem();
            delete.Header = App.Text("TagCM.Delete", tag.Name);
            delete.Icon = this.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteTag(repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareWithSpecial", current.Name);
            compare.Icon = this.CreateMenuIcon("Icons.Compare");
            compare.Click += (_, e) =>
            {
                this.ShowWindow(new ViewModels.Compare(repo, current, tag));
                e.Handled = true;
            };

            submenu.Items.Add(compare);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(tag.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private async Task InteractiveRebaseWithPrefillActionAsync(ViewModels.Repository repo, Models.Commit target, Models.InteractiveRebaseAction action)
        {
            var prefill = new ViewModels.InteractiveRebasePrefill(target.SHA, action);
            var start = action switch
            {
                Models.InteractiveRebaseAction.Squash or Models.InteractiveRebaseAction.Fixup => $"{target.SHA}~~",
                _ => $"{target.SHA}~",
            };

            var on = await new Commands.QuerySingleCommit(repo.FullPath, start).GetResultAsync();
            if (on == null)
                repo.SendNotification($"Commit '{start}' is not a valid revision for `git rebase -i`!", true);
            else
                await this.ShowDialogAsync(new ViewModels.InteractiveRebase(repo, on, prefill));
        }


        private Models.Branch _currentBranch = null;
        private Models.Bisect _bisect = null;
        private AvaloniaList<Models.IssueTracker> _issueTrackers = null;
        private bool _isScrollToTopVisible = false;
        private bool _isDetailsPanelExpanded = true;
        private bool _resizingAuthorColumn = false;
        private Cursor _resizingCursor = new(StandardCursorType.SizeWestEast);
    }
}
