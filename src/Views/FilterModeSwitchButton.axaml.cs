using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            InitializeComponent();
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

            if (DataContext is Models.Tag tag)
            {
                var mode = tag.FilterMode;

                var none = new MenuItem();
                none.Icon = App.CreateMenuIcon("Icons.Eye");
                none.Header = "Default";
                none.IsEnabled = mode != Models.FilterMode.None;
                none.Click += (_, ev) =>
                {
                    UpdateTagFilterMode(repo, tag, Models.FilterMode.None);
                    ev.Handled = true;
                };

                var include = new MenuItem();
                include.Icon = App.CreateMenuIcon("Icons.Filter");
                include.Header = "Filter";
                include.IsEnabled = mode != Models.FilterMode.Included;
                include.Click += (_, ev) =>
                {
                    UpdateTagFilterMode(repo, tag, Models.FilterMode.Included);
                    ev.Handled = true;
                };

                var exclude = new MenuItem();
                exclude.Icon = App.CreateMenuIcon("Icons.EyeClose");
                exclude.Header = "Hide";
                exclude.IsEnabled = mode != Models.FilterMode.Excluded;
                exclude.Click += (_, ev) =>
                {
                    UpdateTagFilterMode(repo, tag, Models.FilterMode.Excluded);
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(none);
                menu.Items.Add(include);
                menu.Items.Add(exclude);

                if (mode == Models.FilterMode.None)
                {
                    IsContextMenuOpening = true;
                    menu.Closed += (_, _) => IsContextMenuOpening = false;
                }

                menu.Open(button);
            }
            else if (DataContext is ViewModels.BranchTreeNode node)
            {
                var mode = node.FilterMode;

                var none = new MenuItem();
                none.Icon = App.CreateMenuIcon("Icons.Eye");
                none.Header = "Default";
                none.IsEnabled = mode != Models.FilterMode.None;
                none.Click += (_, ev) =>
                {
                    UpdateBranchFilterMode(repo, node, Models.FilterMode.None);
                    ev.Handled = true;
                };

                var include = new MenuItem();
                include.Icon = App.CreateMenuIcon("Icons.Filter");
                include.Header = "Filter";
                include.IsEnabled = mode != Models.FilterMode.Included;
                include.Click += (_, ev) =>
                {
                    UpdateBranchFilterMode(repo, node, Models.FilterMode.Included);
                    ev.Handled = true;
                };

                var exclude = new MenuItem();
                exclude.Icon = App.CreateMenuIcon("Icons.EyeClose");
                exclude.Header = "Hide";
                exclude.IsEnabled = mode != Models.FilterMode.Excluded;
                exclude.Click += (_, ev) =>
                {
                    UpdateBranchFilterMode(repo, node, Models.FilterMode.Excluded);
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(none);
                menu.Items.Add(include);
                menu.Items.Add(exclude);

                if (mode == Models.FilterMode.None)
                {
                    IsContextMenuOpening = true;
                    menu.Closed += (_, _) => IsContextMenuOpening = false;
                }

                menu.Open(button);
            }

            e.Handled = true;
        }

        private void UpdateTagFilterMode(ViewModels.Repository repo, Models.Tag tag, Models.FilterMode mode)
        {
            var changed = repo.Settings.UpdateHistoriesFilter(tag.Name, Models.FilterType.Tag, mode);
            if (changed)
            {
                tag.FilterMode = mode;
                Task.Run(repo.RefreshCommits);
            }
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

                node.FilterMode = mode;
            }
            else
            {
                var changed = repo.Settings.UpdateHistoriesFilter(node.Path, isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder, mode);
                if (!changed)
                    return;

                node.FilterMode = mode;
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
                var parent = FindParentNode(tree, parentPath);
                if (parent == null)
                    break;

                repo.Settings.UpdateHistoriesFilter(parent.Path, parentType, Models.FilterMode.None);
                parent.FilterMode = Models.FilterMode.None;
                cur = parent;
            } while (true);

            Task.Run(repo.RefreshCommits);
        }

        private void ResetChildrenBranchNodeFilterMode(ViewModels.Repository repo, ViewModels.BranchTreeNode node, bool isLocal)
        {
            foreach (var child in node.Children)
            {
                child.FilterMode = Models.FilterMode.None;

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

        private ViewModels.BranchTreeNode FindParentNode(List<ViewModels.BranchTreeNode> nodes, string parent)
        {
            foreach (var node in nodes)
            {
                if (node.IsBranch)
                    continue;

                if (node.Path.Equals(parent, StringComparison.Ordinal))
                    return node;

                if (parent.StartsWith(node.Path, StringComparison.Ordinal))
                {
                    var founded = FindParentNode(node.Children, parent);
                    if (founded != null)
                        return founded;
                }
            }

            return null;
        }
    }
}


