using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BranchTreeNode : ObservableObject
    {
        public string Name { get; private set; } = string.Empty;
        public object Backend { get; private set; } = null;
        public int Depth { get; set; } = 0;
        public bool IsFiltered { get; set; } = false;
        public bool IsSelected { get; set; } = false;
        public List<BranchTreeNode> Children { get; private set; } = new List<BranchTreeNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public CornerRadius CornerRadius
        {
            get => _cornerRadius;
            set => SetProperty(ref _cornerRadius, value);
        }

        public bool IsBranch
        {
            get => Backend is Models.Branch;
        }

        public FontWeight NameFontWeight
        {
            get => Backend is Models.Branch { IsCurrent: true } ? FontWeight.Bold : FontWeight.Regular;
        }

        public string Tooltip
        {
            get => Backend is Models.Branch b ? b.FriendlyName : null;
        }

        private bool _isExpanded = false;
        private CornerRadius _cornerRadius = new CornerRadius(4);

        public class Builder
        {
            public List<BranchTreeNode> Locals => _locals;
            public List<BranchTreeNode> Remotes => _remotes;

            public void Run(List<Models.Branch> branches, List<Models.Remote> remotes, bool bForceExpanded)
            {
                var folders = new Dictionary<string, BranchTreeNode>();

                foreach (var remote in remotes)
                {
                    var path = $"remote/{remote.Name}";
                    var node = new BranchTreeNode()
                    {
                        Name = remote.Name,
                        Backend = remote,
                        IsExpanded = bForceExpanded || _expanded.Contains(path),
                    };

                    folders.Add(path, node);
                    _remotes.Add(node);
                }

                foreach (var branch in branches)
                {
                    var isFiltered = _filters.Contains(branch.FullName);
                    if (branch.IsLocal)
                    {
                        MakeBranchNode(branch, _locals, folders, "local", isFiltered, bForceExpanded);
                    }
                    else
                    {
                        var remote = _remotes.Find(x => x.Name == branch.Remote);
                        if (remote != null)
                            MakeBranchNode(branch, remote.Children, folders, $"remote/{remote.Name}", isFiltered, bForceExpanded);
                    }
                }

                folders.Clear();
                SortNodes(_locals);
                SortNodes(_remotes);
            }

            public void SetFilters(AvaloniaList<string> filters)
            {
                _filters.AddRange(filters);
            }

            public void CollectExpandedNodes(List<BranchTreeNode> nodes, bool isLocal)
            {
                CollectExpandedNodes(nodes, isLocal ? "local" : "remote");
            }

            private void CollectExpandedNodes(List<BranchTreeNode> nodes, string prefix)
            {
                foreach (var node in nodes)
                {
                    if (node.Backend is Models.Branch)
                        continue;

                    var path = prefix + "/" + node.Name;
                    if (node.IsExpanded)
                        _expanded.Add(path);

                    CollectExpandedNodes(node.Children, path);
                }
            }

            private void MakeBranchNode(Models.Branch branch, List<BranchTreeNode> roots, Dictionary<string, BranchTreeNode> folders, string prefix, bool isFiltered, bool bForceExpanded)
            {
                var sepIdx = branch.Name.IndexOf('/', StringComparison.Ordinal);
                if (sepIdx == -1)
                {
                    roots.Add(new BranchTreeNode()
                    {
                        Name = branch.Name,
                        Backend = branch,
                        IsExpanded = false,
                        IsFiltered = isFiltered,
                    });
                    return;
                }

                var lastFolder = null as BranchTreeNode;
                var start = 0;

                while (sepIdx != -1)
                {
                    var folder = string.Concat(prefix, "/", branch.Name.Substring(0, sepIdx));
                    var name = branch.Name.Substring(start, sepIdx - start);
                    if (folders.TryGetValue(folder, out var val))
                    {
                        lastFolder = val;
                        if (!lastFolder.IsExpanded)
                            lastFolder.IsExpanded |= (branch.IsCurrent || _expanded.Contains(folder));
                    }
                    else if (lastFolder == null)
                    {
                        lastFolder = new BranchTreeNode()
                        {
                            Name = name,
                            IsExpanded = bForceExpanded || branch.IsCurrent || _expanded.Contains(folder),
                        };
                        roots.Add(lastFolder);
                        folders.Add(folder, lastFolder);
                    }
                    else
                    {
                        var cur = new BranchTreeNode()
                        {
                            Name = name,
                            IsExpanded = bForceExpanded || branch.IsCurrent || _expanded.Contains(folder),
                        };
                        lastFolder.Children.Add(cur);
                        folders.Add(folder, cur);
                        lastFolder = cur;
                    }

                    start = sepIdx + 1;
                    sepIdx = branch.Name.IndexOf('/', start);
                }

                lastFolder?.Children.Add(new BranchTreeNode()
                {
                    Name = Path.GetFileName(branch.Name),
                    Backend = branch,
                    IsExpanded = false,
                    IsFiltered = isFiltered,
                });
            }

            private void SortNodes(List<BranchTreeNode> nodes)
            {
                nodes.Sort((l, r) =>
                {
                    if (l.Backend is Models.Branch { IsDetachedHead: true })
                        return -1;

                    if (l.Backend is Models.Branch)
                        return r.Backend is Models.Branch ? string.Compare(l.Name, r.Name, StringComparison.Ordinal) : 1;

                    return r.Backend is Models.Branch ? -1 : string.Compare(l.Name, r.Name, StringComparison.Ordinal);
                });

                foreach (var node in nodes)
                    SortNodes(node.Children);
            }

            private readonly List<BranchTreeNode> _locals = new List<BranchTreeNode>();
            private readonly List<BranchTreeNode> _remotes = new List<BranchTreeNode>();
            private readonly HashSet<string> _expanded = new HashSet<string>();
            private readonly List<string> _filters = new List<string>();
        }
    }
}
