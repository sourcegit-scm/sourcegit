using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class FilterModeSwitchButton : UserControl
    {
        public static readonly StyledProperty<Models.FilterMode> ModeProperty =
            AvaloniaProperty.Register<FilterModeSwitchButton, Models.FilterMode>(nameof(Mode));

        public Models.FilterMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public static readonly StyledProperty<bool> IsNoneVisibleProperty =
            AvaloniaProperty.Register<FilterModeSwitchButton, bool>(nameof(IsNoneVisible));

        public bool IsNoneVisible
        {
            get => GetValue(IsNoneVisibleProperty);
            set => SetValue(IsNoneVisibleProperty, value);
        }

        public static readonly StyledProperty<bool> IsContextMenuOpeningProperty =
            AvaloniaProperty.Register<FilterModeSwitchButton, bool>(nameof(IsContextMenuOpening));

        public bool IsContextMenuOpening
        {
            get => GetValue(IsContextMenuOpeningProperty);
            set => SetValue(IsContextMenuOpeningProperty, value);
        }

        public FilterModeSwitchButton()
        {
            IsVisible = false;
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ModeProperty ||
                change.Property == IsNoneVisibleProperty ||
                change.Property == IsContextMenuOpeningProperty)
            {
                var visible = (Mode != Models.FilterMode.None || IsNoneVisible || IsContextMenuOpening);
                SetCurrentValue(IsVisibleProperty, visible);
            }
        }

        private void OnChangeFilterModeButtonClicked(object sender, RoutedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView == null)
                return;

            var repo = repoView.DataContext as ViewModels.Repository;
            if (repo == null)
                return;

            var button = sender as Button;
            if (button == null)
                return;

            var menu = new ContextMenu();
            var mode = Models.FilterMode.None;
            if (DataContext is Models.Tag tag)
            {
                mode = tag.FilterMode;

                if (mode != Models.FilterMode.None)
                {
                    var unset = new MenuItem();
                    unset.Header = App.Text("Repository.FilterCommits.Default");
                    unset.Click += (_, ev) =>
                    {
                        UpdateTagFilterMode(repo, tag, Models.FilterMode.None);
                        ev.Handled = true;
                    };

                    menu.Items.Add(unset);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var include = new MenuItem();
                include.Icon = App.CreateMenuIcon("Icons.Filter");
                include.Header = App.Text("Repository.FilterCommits.Include");
                include.IsEnabled = mode != Models.FilterMode.Included;
                include.Click += (_, ev) =>
                {
                    UpdateTagFilterMode(repo, tag, Models.FilterMode.Included);
                    ev.Handled = true;
                };

                var exclude = new MenuItem();
                exclude.Icon = App.CreateMenuIcon("Icons.EyeClose");
                exclude.Header = App.Text("Repository.FilterCommits.Exclude");
                exclude.IsEnabled = mode != Models.FilterMode.Excluded;
                exclude.Click += (_, ev) =>
                {
                    UpdateTagFilterMode(repo, tag, Models.FilterMode.Excluded);
                    ev.Handled = true;
                };

                menu.Items.Add(include);
                menu.Items.Add(exclude);
            }
            else if (DataContext is ViewModels.BranchTreeNode node)
            {
                mode = node.FilterMode;

                if (mode != Models.FilterMode.None)
                {
                    var unset = new MenuItem();
                    unset.Header = App.Text("Repository.FilterCommits.Default");
                    unset.Click += (_, ev) =>
                    {
                        UpdateBranchFilterMode(repo, node, Models.FilterMode.None);
                        ev.Handled = true;
                    };

                    menu.Items.Add(unset);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var include = new MenuItem();
                include.Icon = App.CreateMenuIcon("Icons.Filter");
                include.Header = App.Text("Repository.FilterCommits.Include");
                include.IsEnabled = mode != Models.FilterMode.Included;
                include.Click += (_, ev) =>
                {
                    UpdateBranchFilterMode(repo, node, Models.FilterMode.Included);
                    ev.Handled = true;
                };

                var exclude = new MenuItem();
                exclude.Icon = App.CreateMenuIcon("Icons.EyeClose");
                exclude.Header = App.Text("Repository.FilterCommits.Exclude");
                exclude.IsEnabled = mode != Models.FilterMode.Excluded;
                exclude.Click += (_, ev) =>
                {
                    UpdateBranchFilterMode(repo, node, Models.FilterMode.Excluded);
                    ev.Handled = true;
                };

                menu.Items.Add(include);
                menu.Items.Add(exclude);
            }

            if (mode == Models.FilterMode.None)
            {
                IsContextMenuOpening = true;
                menu.Closed += (_, _) => IsContextMenuOpening = false;
            }

            menu.Open(button);
            e.Handled = true;
        }

        private void UpdateTagFilterMode(ViewModels.Repository repo, Models.Tag tag, Models.FilterMode mode)
        {
            var changed = repo.Settings.UpdateHistoriesFilter(tag.Name, Models.FilterType.Tag, mode);
            repo.MarkHistoriesFilterDirty();
        }

        private void UpdateBranchFilterMode(ViewModels.Repository repo, ViewModels.BranchTreeNode node, Models.FilterMode mode)
        {
            var isLocal = node.Path.StartsWith("refs/heads/", StringComparison.Ordinal);
            var type = isLocal ? Models.FilterType.LocalBranch : Models.FilterType.RemoteBranch;
            var tree = isLocal ? repo.LocalBranchTrees : repo.RemoteBranchTrees;

            if (node.Backend is Models.Branch branch)
            {
                var changed = repo.Settings.UpdateHistoriesFilter(node.Path, type, mode);
                if (!changed)
                    return;

                // Try to update its upstream.
                if (isLocal && !string.IsNullOrEmpty(branch.Upstream) && mode != Models.FilterMode.Excluded)
                {
                    var upstream = branch.Upstream;
                    var canUpdateUpstream = true;
                    foreach (var filter in repo.Settings.HistoriesFilters)
                    {
                        bool matched = false;
                        if (filter.Type == Models.FilterType.RemoteBranch)
                            matched = filter.Pattern.Equals(upstream, StringComparison.Ordinal);
                        else if (filter.Type == Models.FilterType.RemoteBranchFolder)
                            matched = upstream.StartsWith(filter.Pattern, StringComparison.Ordinal);

                        if (matched && filter.Mode == Models.FilterMode.Excluded)
                        {
                            canUpdateUpstream = false;
                            break;
                        }
                    }

                    if (canUpdateUpstream)
                        repo.Settings.UpdateHistoriesFilter(upstream, Models.FilterType.RemoteBranch, mode);
                }
            }
            else
            {
                var changed = repo.Settings.UpdateHistoriesFilter(node.Path, isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder, mode);
                if (!changed)
                    return;

                ResetChildrenBranchNodeFilterMode(repo, node, isLocal);
            }

            var parentType = isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder;
            var cur = node;
            do
            {
                var lastSepIdx = cur.Path.LastIndexOf('/');
                if (lastSepIdx <= 0)
                    break;

                var parentPath = cur.Path.Substring(0, lastSepIdx);
                var parent = FindBranchNode(tree, parentPath);
                if (parent == null)
                    break;

                repo.Settings.UpdateHistoriesFilter(parent.Path, parentType, Models.FilterMode.None);
                cur = parent;
            } while (true);

            repo.MarkHistoriesFilterDirty();
        }

        private void ResetChildrenBranchNodeFilterMode(ViewModels.Repository repo, ViewModels.BranchTreeNode node, bool isLocal)
        {
            foreach (var child in node.Children)
            {
                if (child.IsBranch)
                {
                    var type = isLocal ? Models.FilterType.LocalBranch : Models.FilterType.RemoteBranch;
                    repo.Settings.UpdateHistoriesFilter(child.Path, type, Models.FilterMode.None);
                }
                else
                {
                    var type = isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder;
                    repo.Settings.UpdateHistoriesFilter(child.Path, type, Models.FilterMode.None);
                    ResetChildrenBranchNodeFilterMode(repo, child, isLocal);
                }
            }
        }

        private ViewModels.BranchTreeNode FindBranchNode(List<ViewModels.BranchTreeNode> nodes, string path)
        {
            foreach (var node in nodes)
            {
                if (node.Path.Equals(path, StringComparison.Ordinal))
                    return node;

                if (path.StartsWith(node.Path, StringComparison.Ordinal))
                {
                    var founded = FindBranchNode(node.Children, path);
                    if (founded != null)
                        return founded;
                }
            }

            return null;
        }
    }
}


