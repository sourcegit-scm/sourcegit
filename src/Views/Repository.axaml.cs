using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CounterPresenter : Control
    {
        public static readonly StyledProperty<int> CountProperty =
            AvaloniaProperty.Register<CounterPresenter, int>(nameof(Count), 0);

        public int Count
        {
            get => GetValue(CountProperty);
            set => SetValue(CountProperty, value);
        }

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<CounterPresenter>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<CounterPresenter>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<CounterPresenter, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<CounterPresenter, IBrush>(nameof(Background), Brushes.White);

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        static CounterPresenter()
        {
            AffectsMeasure<CounterPresenter>(
                FontSizeProperty,
                FontFamilyProperty,
                ForegroundProperty,
                CountProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_label != null)
            {
                context.DrawRectangle(Background, null, new RoundedRect(new Rect(0, 0, _label.Width + 18, 18), new CornerRadius(9)));
                context.DrawText(_label, new Point(9, 9 - _label.Height * 0.5));
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Count > 0)
            {
                _label = new FormattedText(
                    Count.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily),
                    FontSize,
                    Foreground);
            }
            else
            {
                _label = null;
            }

            InvalidateVisual();
            return _label != null ? new Size(_label.Width + 18, 18) : new Size(0, 0);
        }

        private FormattedText _label = null;
    }

    public partial class Repository : UserControl
    {
        public Repository()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateLeftSidebarLayout();
        }

        private void OnSearchCommitPanelPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty && sender is Grid { IsVisible: true })
                TxtSearchCommitsBox.Focus();
        }

        private void OnSearchKeyDown(object _, KeyEventArgs e)
        {
            var repo = DataContext as ViewModels.Repository;
            if (repo == null)
                return;

            if (e.Key == Key.Enter)
            {
                if (!string.IsNullOrWhiteSpace(repo.SearchCommitFilter))
                    repo.StartSearchCommits();

                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (repo.IsSearchCommitSuggestionOpen)
                {
                    SearchSuggestionBox.Focus(NavigationMethod.Tab);
                    SearchSuggestionBox.SelectedIndex = 0;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (repo.IsSearchCommitSuggestionOpen)
                {
                    repo.SearchCommitFilterSuggestion.Clear();
                    repo.IsSearchCommitSuggestionOpen = false;
                }

                e.Handled = true;
            }
        }

        private void OnBranchTreeRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void OnLocalBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            RemoteBranchTree.UnselectAll();
            TagsList.UnselectAll();
        }

        private void OnRemoteBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            TagsList.UnselectAll();
        }

        private void OnTagsRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void OnTagsSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            RemoteBranchTree.UnselectAll();
        }

        private void OnSubmoduleContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: Models.Submodule submodule } grid && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForSubmodule(submodule.Path);
                menu?.Open(grid);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedSubmodule(object sender, TappedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: Models.Submodule submodule } && DataContext is ViewModels.Repository repo)
            {
                repo.OpenSubmodule(submodule.Path);
            }

            e.Handled = true;
        }

        private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: Models.Worktree worktree } grid && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForWorktree(worktree);
                menu?.Open(grid);
            }

            e.Handled = true;
        }

        private void OnDoubleTappedWorktree(object sender, TappedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: Models.Worktree worktree } && DataContext is ViewModels.Repository repo)
            {
                repo.OpenWorktree(worktree);
            }

            e.Handled = true;
        }

        private void OnLeftSidebarListBoxPropertyChanged(object _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ListBox.ItemsSourceProperty || e.Property == ListBox.IsVisibleProperty)
                UpdateLeftSidebarLayout();
        }

        private void OnLeftSidebarSizeChanged(object _, SizeChangedEventArgs e)
        {
            if (e.HeightChanged)
                UpdateLeftSidebarLayout();
        }

        private void UpdateLeftSidebarLayout()
        {
            var vm = DataContext as ViewModels.Repository;
            if (vm == null || vm.Settings == null)
                return;

            if (!IsLoaded)
                return;

            var leftHeight = LeftSidebarGroups.Bounds.Height - 28.0 * 5 - 4;
            if (leftHeight <= 0)
                return;

            var localBranchRows = vm.IsLocalBranchGroupExpanded ? LocalBranchTree.Rows.Count : 0;
            var remoteBranchRows = vm.IsRemoteGroupExpanded ? RemoteBranchTree.Rows.Count : 0;
            var desiredBranches = (localBranchRows + remoteBranchRows) * 24.0;
            var desiredTag = vm.IsTagGroupExpanded ? 24.0 * TagsList.Rows : 0;
            var desiredSubmodule = vm.IsSubmoduleGroupExpanded ? 24.0 * vm.VisibleSubmodules.Count : 0;
            var desiredWorktree = vm.IsWorktreeGroupExpanded ? 24.0 * vm.Worktrees.Count : 0;
            var desiredOthers = desiredTag + desiredSubmodule + desiredWorktree;
            var hasOverflow = (desiredBranches + desiredOthers > leftHeight);

            if (vm.IsTagGroupExpanded)
            {
                var height = desiredTag;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredSubmodule - desiredWorktree;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                TagsList.Height = height;
                hasOverflow = (desiredBranches + desiredSubmodule + desiredWorktree) > leftHeight;
            }

            if (vm.IsSubmoduleGroupExpanded)
            {
                var height = desiredSubmodule;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches - desiredWorktree;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                SubmoduleList.Height = height;
                hasOverflow = (desiredBranches + desiredWorktree) > leftHeight;
            }

            if (vm.IsWorktreeGroupExpanded)
            {
                var height = desiredWorktree;
                if (hasOverflow)
                {
                    var test = leftHeight - desiredBranches;
                    if (test < 0)
                        height = Math.Min(200, height);
                    else
                        height = Math.Max(200, test);
                }

                leftHeight -= height;
                WorktreeList.Height = height;
            }

            if (leftHeight > 0 && desiredBranches > leftHeight)
            {
                var local = localBranchRows * 24.0;
                var remote = remoteBranchRows * 24.0;
                var half = leftHeight / 2;
                if (vm.IsLocalBranchGroupExpanded)
                {
                    if (vm.IsRemoteGroupExpanded)
                    {
                        if (local < half)
                        {
                            LocalBranchTree.Height = local;
                            RemoteBranchTree.Height = leftHeight - local;
                        }
                        else if (remote < half)
                        {
                            RemoteBranchTree.Height = remote;
                            LocalBranchTree.Height = leftHeight - remote;
                        }
                        else
                        {
                            LocalBranchTree.Height = half;
                            RemoteBranchTree.Height = half;
                        }
                    }
                    else
                    {
                        LocalBranchTree.Height = leftHeight;
                    }
                }
                else if (vm.IsRemoteGroupExpanded)
                {
                    RemoteBranchTree.Height = leftHeight;
                }
            }
            else
            {
                if (vm.IsLocalBranchGroupExpanded)
                {
                    var height = localBranchRows * 24;
                    LocalBranchTree.Height = height;
                }

                if (vm.IsRemoteGroupExpanded)
                {
                    var height = remoteBranchRows * 24;
                    RemoteBranchTree.Height = height;
                }
            }
        }

        private void OnSearchSuggestionBoxKeyDown(object _, KeyEventArgs e)
        {
            var repo = DataContext as ViewModels.Repository;
            if (repo == null)
                return;

            if (e.Key == Key.Escape)
            {
                repo.IsSearchCommitSuggestionOpen = false;
                repo.SearchCommitFilterSuggestion.Clear();

                e.Handled = true;
            }
            else if (e.Key == Key.Enter && SearchSuggestionBox.SelectedItem is string content)
            {
                repo.SearchCommitFilter = content;
                TxtSearchCommitsBox.CaretIndex = content.Length;
                repo.StartSearchCommits();
                e.Handled = true;
            }
        }

        private void OnSearchSuggestionDoubleTapped(object sender, TappedEventArgs e)
        {
            var repo = DataContext as ViewModels.Repository;
            if (repo == null)
                return;

            var content = (sender as StackPanel)?.DataContext as string;
            if (!string.IsNullOrEmpty(content))
            {
                repo.SearchCommitFilter = content;
                TxtSearchCommitsBox.CaretIndex = content.Length;
                repo.StartSearchCommits();
            }
            e.Handled = true;
        }

        private void OnOpenAdvancedHistoriesOption(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var layout = new MenuItem();
                layout.Header = App.Text("Repository.HistoriesLayout");
                layout.IsEnabled = false;

                var isHorizontal = ViewModels.Preferences.Instance.UseTwoColumnsLayoutInHistories;
                var horizontal = new MenuItem();
                horizontal.Header = App.Text("Repository.HistoriesLayout.Horizontal");
                if (isHorizontal)
                    horizontal.Icon = App.CreateMenuIcon("Icons.Check");
                horizontal.Click += (_, ev) =>
                {
                    ViewModels.Preferences.Instance.UseTwoColumnsLayoutInHistories = true;
                    ev.Handled = true;
                };

                var vertical = new MenuItem();
                vertical.Header = App.Text("Repository.HistoriesLayout.Vertical");
                if (!isHorizontal)
                    vertical.Icon = App.CreateMenuIcon("Icons.Check");
                vertical.Click += (_, ev) =>
                {
                    ViewModels.Preferences.Instance.UseTwoColumnsLayoutInHistories = false;
                    ev.Handled = true;
                };

                var order = new MenuItem();
                order.Header = App.Text("Repository.HistoriesOrder");
                order.IsEnabled = false;

                var dateOrder = new MenuItem();
                dateOrder.Header = App.Text("Repository.HistoriesOrder.ByDate");
                if (!repo.EnableTopoOrderInHistories)
                    dateOrder.Icon = App.CreateMenuIcon("Icons.Check");
                dateOrder.Click += (_, ev) =>
                {
                    repo.EnableTopoOrderInHistories = false;
                    ev.Handled = true;
                };

                var topoOrder = new MenuItem();
                topoOrder.Header = App.Text("Repository.HistoriesOrder.Topo");
                if (repo.EnableTopoOrderInHistories)
                    topoOrder.Icon = App.CreateMenuIcon("Icons.Check");
                topoOrder.Click += (_, ev) =>
                {
                    repo.EnableTopoOrderInHistories = true;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(layout);
                menu.Items.Add(horizontal);
                menu.Items.Add(vertical);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(order);
                menu.Items.Add(dateOrder);
                menu.Items.Add(topoOrder);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenSortTagMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var byCreatorDate = new MenuItem();
                byCreatorDate.Header = App.Text("Repository.Tags.OrderByCreatorDate");
                if (repo.TagSortMode == Models.TagSortMode.CreatorDate)
                    byCreatorDate.Icon = App.CreateMenuIcon("Icons.Check");
                byCreatorDate.Click += (_, ev) =>
                {
                    repo.TagSortMode = Models.TagSortMode.CreatorDate;
                    ev.Handled = true;
                };

                var byNameAsc = new MenuItem();
                byNameAsc.Header = App.Text("Repository.Tags.OrderByNameAsc");
                if (repo.TagSortMode == Models.TagSortMode.NameInAscending)
                    byNameAsc.Icon = App.CreateMenuIcon("Icons.Check");
                byNameAsc.Click += (_, ev) =>
                {
                    repo.TagSortMode = Models.TagSortMode.NameInAscending;
                    ev.Handled = true;
                };

                var byNameDes = new MenuItem();
                byNameDes.Header = App.Text("Repository.Tags.OrderByNameDes");
                if (repo.TagSortMode == Models.TagSortMode.NameInDescending)
                    byNameDes.Icon = App.CreateMenuIcon("Icons.Check");
                byNameDes.Click += (_, ev) =>
                {
                    repo.TagSortMode = Models.TagSortMode.NameInDescending;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(byCreatorDate);
                menu.Items.Add(byNameAsc);
                menu.Items.Add(byNameDes);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnSkipInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                repo.SkipMerge();

            e.Handled = true;
        }
    }
}
