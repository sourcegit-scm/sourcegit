using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Welcome : ObservableObject
    {
        public static Welcome Instance { get; } = new();

        public AvaloniaList<RepositoryNode> Rows
        {
            get;
            private set;
        } = [];

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    Refresh();
            }
        }

        public Welcome()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (string.IsNullOrWhiteSpace(_searchFilter))
            {
                foreach (var node in Preferences.Instance.RepositoryNodes)
                    ResetVisibility(node);
            }
            else
            {
                foreach (var node in Preferences.Instance.RepositoryNodes)
                    SetVisibilityBySearch(node);
            }

            var rows = new List<RepositoryNode>();
            MakeTreeRows(rows, Preferences.Instance.RepositoryNodes);
            Rows.Clear();
            Rows.AddRange(rows);
        }

        public async Task UpdateStatusAsync(bool force, CancellationToken? token)
        {
            if (_isUpdatingStatus)
                return;

            _isUpdatingStatus = true;

            // avoid collection was modified while enumerating.
            var nodes = new List<RepositoryNode>();
            nodes.AddRange(Preferences.Instance.RepositoryNodes);

            foreach (var node in nodes)
                await node.UpdateStatusAsync(force, token);

            _isUpdatingStatus = false;
        }

        public void ToggleNodeIsExpanded(RepositoryNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            var depth = node.Depth;
            var idx = Rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subrows = new List<RepositoryNode>();
                MakeTreeRows(subrows, node.SubNodes, depth + 1);
                Rows.InsertRange(idx + 1, subrows);
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < Rows.Count; i++)
                {
                    var row = Rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }
                Rows.RemoveRange(idx + 1, removeCount);
            }
        }

        public async Task<string> GetRepositoryRootAsync(string path)
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return null;
            }

            var root = path;
            if (!Directory.Exists(root))
            {
                if (File.Exists(root))
                    root = Path.GetDirectoryName(root);
                else
                    return null;
            }

            var isBare = await new Commands.IsBareRepository(root).GetResultAsync();
            if (isBare)
                return root;

            var rs = await new Commands.QueryRepositoryRootPath(root).GetResultAsync();
            if (!rs.IsSuccess || string.IsNullOrWhiteSpace(rs.StdOut))
                return null;

            return rs.StdOut.Trim();
        }

        public void InitRepository(string path, RepositoryNode parent, string reason)
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new Init(activePage.Node.Id, path, parent, reason);
        }

        public async Task AddRepositoryAsync(string path, RepositoryNode parent, bool moveNode, bool open)
        {
            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(path, parent, moveNode);
            await node.UpdateStatusAsync(false, null);

            if (open)
                node.Open();
        }

        public void Clone()
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new Clone(activePage.Node.Id);
        }

        public void OpenTerminal()
        {
            if (!Preferences.Instance.IsGitConfigured())
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
            else
                Native.OS.OpenTerminal(null);
        }

        public void ScanDefaultCloneDir()
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                App.RaiseException(string.Empty, App.Text("NotConfigured"));
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new ScanRepositories();
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public void AddRootNode()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new CreateGroup(null);
        }

        public RepositoryNode FindNodeById(string id, RepositoryNode root = null)
        {
            var collection = (root == null) ? Preferences.Instance.RepositoryNodes : root.SubNodes;
            foreach (var node in collection)
            {
                if (node.Id.Equals(id, StringComparison.Ordinal))
                    return node;

                var sub = FindNodeById(id, node);
                if (sub != null)
                    return sub;
            }

            return null;
        }

        public RepositoryNode FindParentGroup(RepositoryNode node, RepositoryNode group = null)
        {
            var collection = (group == null) ? Preferences.Instance.RepositoryNodes : group.SubNodes;
            if (collection.Contains(node))
                return group;

            foreach (var item in collection)
            {
                if (!item.IsRepository)
                {
                    var parent = FindParentGroup(node, item);
                    if (parent != null)
                        return parent;
                }
            }

            return null;
        }

        public void MoveNode(RepositoryNode from, RepositoryNode to)
        {
            Preferences.Instance.MoveNode(from, to, true);
            Refresh();
        }

        private void ResetVisibility(RepositoryNode node)
        {
            node.IsVisible = true;
            foreach (var subNode in node.SubNodes)
                ResetVisibility(subNode);
        }

        private void SetVisibilityBySearch(RepositoryNode node)
        {
            if (!node.IsRepository)
            {
                if (node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    node.IsVisible = true;
                    foreach (var subNode in node.SubNodes)
                        ResetVisibility(subNode);
                }
                else
                {
                    bool hasVisibleSubNode = false;
                    foreach (var subNode in node.SubNodes)
                    {
                        SetVisibilityBySearch(subNode);
                        hasVisibleSubNode |= subNode.IsVisible;
                    }
                    node.IsVisible = hasVisibleSubNode;
                }
            }
            else
            {
                node.IsVisible = node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void MakeTreeRows(List<RepositoryNode> rows, List<RepositoryNode> nodes, int depth = 0)
        {
            foreach (var node in nodes)
            {
                if (!node.IsVisible)
                    continue;

                node.Depth = depth;
                rows.Add(node);

                if (node.IsRepository || !node.IsExpanded)
                    continue;

                MakeTreeRows(rows, node.SubNodes, depth + 1);
            }
        }

        private string _searchFilter = string.Empty;
        private bool _isUpdatingStatus = false;
    }
}
