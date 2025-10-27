using System;
using System.Collections.Generic;
using System.Text;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        public static readonly StyledProperty<AvaloniaList<Models.IssueTracker>> IssueTrackersProperty =
            AvaloniaProperty.Register<Histories, AvaloniaList<Models.IssueTracker>>(nameof(IssueTrackers));

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => GetValue(IssueTrackersProperty);
            set => SetValue(IssueTrackersProperty, value);
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

        public static readonly StyledProperty<bool> IsScrollToTopVisibleProperty =
            AvaloniaProperty.Register<Histories, bool>(nameof(IsScrollToTopVisible));

        public bool IsScrollToTopVisible
        {
            get => GetValue(IsScrollToTopVisibleProperty);
            set => SetValue(IsScrollToTopVisibleProperty, value);
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

            SetCurrentValue(IsScrollToTopVisibleProperty, startY >= rowHeight);

            var clipWidth = dataGrid.Columns[0].ActualWidth - 4;
            if (Math.Abs(_lastGraphStartY - startY) > 0.01 ||
                Math.Abs(_lastGraphClipWidth - clipWidth) > 0.01 ||
                Math.Abs(_lastGraphRowHeight - rowHeight) > 0.01)
            {
                _lastGraphStartY = startY;
                _lastGraphClipWidth = clipWidth;
                _lastGraphRowHeight = rowHeight;

                CommitGraph.Layout = new(startY, clipWidth, rowHeight);
            }
        }

        private void OnScrollToTopPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories)
                CommitListContainer.ScrollIntoView(histories.Commits[0], null);
        }

        private void OnCommitListSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories)
                histories.Select(CommitListContainer.SelectedItems);

            e.Handled = true;
        }

        private void OnCommitListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DataGrid { SelectedItems: { } selected } dataGrid &&
                e.Source is Control { DataContext: Models.Commit })
            {
                var repoView = this.FindAncestorOfType<Repository>();
                if (repoView is not { DataContext: ViewModels.Repository repo })
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
                    menu.Open(dataGrid);
                }
                else if (selected.Count == 1)
                {
                    var menu = CreateContextMenuForSingleCommit(repo, commits[0]);
                    menu.Open(dataGrid);
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
                }
            }
        }

        private async void OnCommitListDoubleTapped(object sender, TappedEventArgs e)
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
                    var succ = await histories.CheckoutBranchByDecoratorAsync(decorator);
                    if (succ)
                        return;
                }

                if (e.Source is Control { DataContext: Models.Commit c })
                    await histories.CheckoutBranchByCommitAsync(c);
            }
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
                    cherryPick.Icon = App.CreateMenuIcon("Icons.CherryPick");
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
                    merge.Icon = App.CreateMenuIcon("Icons.Merge");
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
            saveToPatch.Icon = App.CreateMenuIcon("Icons.Diff");
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
                            App.SendNotification(repo.FullPath, App.Text("SaveAsPatchSuccess"));
                    }
                }
                catch (Exception exception)
                {
                    App.RaiseException(repo.FullPath, $"Failed to save as patch: {exception.Message}");
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copyShas = new MenuItem();
            copyShas.Header = App.Text("CommitCM.CopySHA");
            copyShas.Icon = App.CreateMenuIcon("Icons.Hash");
            copyShas.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.AppendLine(c.SHA);

                await App.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copyInfos = new MenuItem();
            copyInfos.Header = App.Text("CommitCM.CopySHA") + " - " + App.Text("CommitCM.CopySubject");
            copyInfos.Icon = App.CreateMenuIcon("Icons.ShaAndSubject");
            copyInfos.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyInfos.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.Append(c.SHA.AsSpan(0, 10)).Append(" - ").AppendLine(c.Subject);

                await App.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copyMessage = new MenuItem();
            copyMessage.Header = App.Text("CommitCM.CopyCommitMessage");
            copyMessage.Icon = App.CreateMenuIcon("Icons.Info");
            copyMessage.Click += async (_, e) =>
            {
                var vm = DataContext as ViewModels.Histories;
                var messages = new List<string>();
                foreach (var c in selected)
                {
                    var message = await vm.GetCommitFullMessageAsync(c);
                    messages.Add(message);
                }

                await App.CopyTextAsync(string.Join("\n-----\n", messages));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Items.Add(copyShas);
            copy.Items.Add(copyInfos);
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
                            var lb = repo.Branches.Find(x => x.IsLocal && d.Name == x.Name);
                            FillOtherLocalBranchMenu(menu, repo, lb, current, commit.IsMerged);
                            break;
                        case Models.DecoratorType.RemoteBranchHead:
                            var rb = repo.Branches.Find(x => !x.IsLocal && d.Name == x.FriendlyName);
                            FillRemoteBranchMenu(menu, repo, rb, current, commit.IsMerged);
                            break;
                        case Models.DecoratorType.Tag:
                            var t = repo.Tags.Find(x => x.Name == d.Name);
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
                    FillTagMenu(menu, repo, tag, current, commit.IsMerged);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var createBranch = new MenuItem();
            createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
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
            createTag.Icon = App.CreateMenuIcon("Icons.Tag.Add");
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

                if (isHead)
                {
                    var reword = new MenuItem();
                    reword.Header = App.Text("CommitCM.Reword");
                    reword.Icon = App.CreateMenuIcon("Icons.Edit");
                    reword.Click += async (_, e) =>
                    {
                        await vm.RewordHeadAsync(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(reword);

                    var squash = new MenuItem();
                    squash.Header = App.Text("CommitCM.Squash");
                    squash.Icon = App.CreateMenuIcon("Icons.SquashIntoParent");
                    squash.IsEnabled = commit.Parents.Count == 1;
                    squash.Click += async (_, e) =>
                    {
                        await vm.SquashHeadAsync(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(squash);
                }
                else
                {
                    var reset = new MenuItem();
                    reset.Header = App.Text("CommitCM.Reset", current.Name, target);
                    reset.Icon = App.CreateMenuIcon("Icons.Reset");
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
                    rebase.Icon = App.CreateMenuIcon("Icons.Rebase");
                    rebase.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.Rebase(repo, current, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(rebase);

                    if (!commit.HasDecorators)
                    {
                        var merge = new MenuItem();
                        merge.Header = App.Text("CommitCM.Merge", current.Name);
                        merge.Icon = App.CreateMenuIcon("Icons.Merge");
                        merge.Click += (_, e) =>
                        {
                            if (repo.CanCreatePopup())
                                repo.ShowPopup(new ViewModels.Merge(repo, commit, current.Name));

                            e.Handled = true;
                        };
                        menu.Items.Add(merge);
                    }

                    var cherryPick = new MenuItem();
                    cherryPick.Header = App.Text("CommitCM.CherryPick");
                    cherryPick.Icon = App.CreateMenuIcon("Icons.CherryPick");
                    cherryPick.Click += async (_, e) =>
                    {
                        await vm.CherryPickAsync(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(cherryPick);
                }

                var revert = new MenuItem();
                revert.Header = App.Text("CommitCM.Revert");
                revert.Icon = App.CreateMenuIcon("Icons.Undo");
                revert.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Revert(repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(revert);

                if (isHead)
                {
                    var dropHead = new MenuItem();
                    dropHead.Header = App.Text("CommitCM.Drop");
                    dropHead.Icon = App.CreateMenuIcon("Icons.Clear");
                    dropHead.Click += async (_, e) =>
                    {
                        await vm.DropHeadAsync(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(dropHead);
                }
                else
                {
                    var checkoutCommit = new MenuItem();
                    checkoutCommit.Header = App.Text("CommitCM.Checkout");
                    checkoutCommit.Icon = App.CreateMenuIcon("Icons.Detached");
                    checkoutCommit.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.CheckoutCommit(repo, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(checkoutCommit);

                    if (commit.IsMerged && commit.Parents.Count > 0)
                    {
                        var manually = new MenuItem();
                        manually.Header = App.Text("CommitCM.InteractiveRebase.Manually", current.Name, target);
                        manually.Icon = App.CreateMenuIcon("Icons.InteractiveRebase");
                        manually.Click += async (_, e) =>
                        {
                            await App.ShowDialog(new ViewModels.InteractiveRebase(repo, commit));
                            e.Handled = true;
                        };

                        var reword = new MenuItem();
                        reword.Header = App.Text("CommitCM.InteractiveRebase.Reword");
                        reword.Icon = App.CreateMenuIcon("Icons.Rename");
                        reword.Click += async (_, e) =>
                        {
                            await vm.InteractiveRebaseAsync(commit, Models.InteractiveRebaseAction.Reword);
                            e.Handled = true;
                        };

                        var edit = new MenuItem();
                        edit.Header = App.Text("CommitCM.InteractiveRebase.Edit");
                        edit.Icon = App.CreateMenuIcon("Icons.Edit");
                        edit.Click += async (_, e) =>
                        {
                            await vm.InteractiveRebaseAsync(commit, Models.InteractiveRebaseAction.Edit);
                            e.Handled = true;
                        };

                        var squash = new MenuItem();
                        squash.Header = App.Text("CommitCM.InteractiveRebase.Squash");
                        squash.Icon = App.CreateMenuIcon("Icons.SquashIntoParent");
                        squash.Click += async (_, e) =>
                        {
                            await vm.InteractiveRebaseAsync(commit, Models.InteractiveRebaseAction.Squash);
                            e.Handled = true;
                        };

                        var fixup = new MenuItem();
                        fixup.Header = App.Text("CommitCM.InteractiveRebase.Fixup");
                        fixup.Icon = App.CreateMenuIcon("Icons.Fix");
                        fixup.Click += async (_, e) =>
                        {
                            await vm.InteractiveRebaseAsync(commit, Models.InteractiveRebaseAction.Fixup);
                            e.Handled = true;
                        };

                        var drop = new MenuItem();
                        drop.Header = App.Text("CommitCM.InteractiveRebase.Drop");
                        drop.Icon = App.CreateMenuIcon("Icons.Clear");
                        drop.Click += async (_, e) =>
                        {
                            await vm.InteractiveRebaseAsync(commit, Models.InteractiveRebaseAction.Drop);
                            e.Handled = true;
                        };

                        var interactiveRebase = new MenuItem();
                        interactiveRebase.Header = App.Text("CommitCM.InteractiveRebase");
                        interactiveRebase.Icon = App.CreateMenuIcon("Icons.InteractiveRebase");
                        interactiveRebase.Items.Add(manually);
                        interactiveRebase.Items.Add(new MenuItem() { Header = "-" });
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
                        interactiveRebase.Icon = App.CreateMenuIcon("Icons.InteractiveRebase");
                        interactiveRebase.Click += async (_, e) =>
                        {
                            await App.ShowDialog(new ViewModels.InteractiveRebase(repo, commit));
                            e.Handled = true;
                        };

                        menu.Items.Add(new MenuItem() { Header = "-" });
                        menu.Items.Add(interactiveRebase);
                    }
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
                    pushRevision.Icon = App.CreateMenuIcon("Icons.Push");
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
                compareWithHead.Icon = App.CreateMenuIcon("Icons.Compare");
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
                    compareWithWorktree.Icon = App.CreateMenuIcon("Icons.Compare");
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
            saveToPatch.Icon = App.CreateMenuIcon("Icons.Diff");
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
                        await repo.SaveCommitAsPatchAsync(commit, folderPath);
                    }
                }
                catch (Exception exception)
                {
                    App.RaiseException(repo.FullPath, $"Failed to save as patch: {exception.Message}");
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
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
                custom.Icon = App.CreateMenuIcon("Icons.Action");

                foreach (var action in actions)
                {
                    var (dup, label) = action;
                    var item = new MenuItem();
                    item.Icon = App.CreateMenuIcon("Icons.Action");
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

            var copySHA = new MenuItem();
            copySHA.Header = App.Text("CommitCM.CopySHA");
            copySHA.Icon = App.CreateMenuIcon("Icons.Hash");
            copySHA.Click += async (_, e) =>
            {
                await App.CopyTextAsync(commit.SHA);
                e.Handled = true;
            };

            var copySubject = new MenuItem();
            copySubject.Header = App.Text("CommitCM.CopySubject");
            copySubject.Icon = App.CreateMenuIcon("Icons.Subject");
            copySubject.Click += async (_, e) =>
            {
                await App.CopyTextAsync(commit.Subject);
                e.Handled = true;
            };

            var copyInfo = new MenuItem();
            copyInfo.Header = App.Text("CommitCM.CopySHA") + " - " + App.Text("CommitCM.CopySubject");
            copyInfo.Icon = App.CreateMenuIcon("Icons.ShaAndSubject");
            copyInfo.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyInfo.Click += async (_, e) =>
            {
                await App.CopyTextAsync($"{commit.SHA.AsSpan(0, 10)} - {commit.Subject}");
                e.Handled = true;
            };

            var copyMessage = new MenuItem();
            copyMessage.Header = App.Text("CommitCM.CopyCommitMessage");
            copyMessage.Icon = App.CreateMenuIcon("Icons.Info");
            copyMessage.Click += async (_, e) =>
            {
                var message = await vm.GetCommitFullMessageAsync(commit);
                await App.CopyTextAsync(message);
                e.Handled = true;
            };

            var copyAuthor = new MenuItem();
            copyAuthor.Header = App.Text("CommitCM.CopyAuthor");
            copyAuthor.Icon = App.CreateMenuIcon("Icons.User");
            copyAuthor.Click += async (_, e) =>
            {
                await App.CopyTextAsync(commit.Author.ToString());
                e.Handled = true;
            };

            var copyCommitter = new MenuItem();
            copyCommitter.Header = App.Text("CommitCM.CopyCommitter");
            copyCommitter.Icon = App.CreateMenuIcon("Icons.User");
            copyCommitter.Click += async (_, e) =>
            {
                await App.CopyTextAsync(commit.Committer.ToString());
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Items.Add(copySHA);
            copy.Items.Add(copySubject);
            copy.Items.Add(copyInfo);
            copy.Items.Add(copyMessage);
            copy.Items.Add(copyAuthor);
            copy.Items.Add(copyCommitter);
            menu.Items.Add(copy);

            return menu;
        }

        private void FillCurrentBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
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
                fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
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
                pull.Icon = App.CreateMenuIcon("Icons.Pull");
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
            push.Icon = App.CreateMenuIcon("Icons.Push");
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
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
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
                    finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
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
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await App.CopyTextAsync(current.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillOtherLocalBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch branch, Models.Branch current, bool merged)
        {
            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
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
                checkout.Icon = App.CreateMenuIcon("Icons.Check");
                checkout.Click += async (_, e) =>
                {
                    await repo.CheckoutBranchAsync(branch);
                    e.Handled = true;
                };
                submenu.Items.Add(checkout);

                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", branch.Name, current.Name);
                merge.Icon = App.CreateMenuIcon("Icons.Merge");
                merge.IsEnabled = !merged;
                merge.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Merge(repo, branch, current.Name, false));
                    e.Handled = true;
                };
                submenu.Items.Add(merge);
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.RenameBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
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
                    finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
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

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await App.CopyTextAsync(branch.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillRemoteBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch branch, Models.Branch current, bool merged)
        {
            var name = branch.FriendlyName;

            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
            submenu.Header = name;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, branch);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", name);
            checkout.Icon = App.CreateMenuIcon("Icons.Check");
            checkout.Click += async (_, e) =>
            {
                await repo.CheckoutBranchAsync(branch);
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = App.Text("BranchCM.Merge", name, current.Name);
            merge.Icon = App.CreateMenuIcon("Icons.Merge");
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
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await App.CopyTextAsync(name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillTagMenu(ContextMenu menu, ViewModels.Repository repo, Models.Tag tag, Models.Branch current, bool merged)
        {
            var submenu = new MenuItem();
            submenu.Header = tag.Name;
            submenu.Icon = App.CreateMenuIcon("Icons.Tag");
            submenu.MinWidth = 200;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, tag);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var push = new MenuItem();
            push.Header = App.Text("TagCM.Push", tag.Name);
            push.Icon = App.CreateMenuIcon("Icons.Push");
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.PushTag(repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            if (!repo.IsBare && !merged)
            {
                var merge = new MenuItem();
                merge.Header = App.Text("TagCM.Merge", tag.Name, current.Name);
                merge.Icon = App.CreateMenuIcon("Icons.Merge");
                merge.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Merge(repo, tag, current.Name));
                    e.Handled = true;
                };
                submenu.Items.Add(merge);
            }

            var delete = new MenuItem();
            delete.Header = App.Text("TagCM.Delete", tag.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteTag(repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await App.CopyTextAsync(tag.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private double _lastGraphStartY = 0;
        private double _lastGraphClipWidth = 0;
        private double _lastGraphRowHeight = 0;
    }
}
