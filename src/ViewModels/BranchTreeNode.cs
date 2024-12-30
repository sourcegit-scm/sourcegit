using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BranchTreeNode : ObservableObject
    {
        public string Name { get; private set; } = string.Empty;
        public string Path { get; private set; } = string.Empty;
        public object Backend { get; private set; } = null;
        public int Depth { get; set; } = 0;
        public bool IsSelected { get; set; } = false;
        public List<BranchTreeNode> Children { get; private set; } = new List<BranchTreeNode>();

        public Models.FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

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

        private Models.FilterMode _filterMode = Models.FilterMode.None;
        private bool _isExpanded = false;
        private CornerRadius _cornerRadius = new CornerRadius(4);

        public class Builder
        {
            public List<BranchTreeNode> Locals => _locals;
            public List<BranchTreeNode> Remotes => _remotes;
            public List<string> InvalidExpandedNodes => _invalidExpandedNodes;

            public void SetExpandedNodes(List<string> expanded)
            {
                foreach (var node in expanded)
                    _expanded.Add(node);
            }

            public void Run(List<Models.Branch> branches, List<Models.Remote> remotes, bool bForceExpanded)
            {
                var folders = new Dictionary<string, BranchTreeNode>();

                foreach (var remote in remotes)
                {
                    var path = $"refs/remotes/{remote.Name}";
                    var node = new BranchTreeNode()
                    {
                        Name = remote.Name,
                        Path = path,
                        Backend = remote,
                        IsExpanded = bForceExpanded || _expanded.Contains(path),
                    };

                    folders.Add(path, node);
                    _remotes.Add(node);
                }

                foreach (var branch in branches)
                {
                    if (branch.IsLocal)
                    {
                        MakeBranchNode(branch, _locals, folders, "refs/heads", bForceExpanded);
                    }
                    else
                    {
                        var remote = _remotes.Find(x => x.Name == branch.Remote);
                        if (remote != null)
                            MakeBranchNode(branch, remote.Children, folders, $"refs/remotes/{remote.Name}", bForceExpanded);
                    }
                }

                foreach (var path in _expanded)
                {
                    if (!folders.ContainsKey(path))
                        _invalidExpandedNodes.Add(path);
                }

                folders.Clear();
                SortNodes(_locals);
                SortNodes(_remotes);
            }

            private void MakeBranchNode(Models.Branch branch, List<BranchTreeNode> roots, Dictionary<string, BranchTreeNode> folders, string prefix, bool bForceExpanded)
            {
                var fullpath = $"{prefix}/{branch.Name}";
                var sepIdx = branch.Name.IndexOf('/', StringComparison.Ordinal);
                if (sepIdx == -1 || branch.IsDetachedHead)
                {
                    roots.Add(new BranchTreeNode()
                    {
                        Name = branch.Name,
                        Path = fullpath,
                        Backend = branch,
                        IsExpanded = false,
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
                            Path = folder,
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
                            Path = folder,
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
                    Name = System.IO.Path.GetFileName(branch.Name),
                    Path = fullpath,
                    Backend = branch,
                    IsExpanded = false,
                });
            }

            private void SortNodes(List<BranchTreeNode> nodes)
            {
                nodes.Sort((l, r) =>
                {
                    if (l.Backend is Models.Branch { IsDetachedHead: true })
                        return -1;

                    if (l.Backend is Models.Branch)
                        return r.Backend is Models.Branch ? Models.NumericSort.Compare(l.Name, r.Name) : 1;

                    return r.Backend is Models.Branch ? -1 : Models.NumericSort.Compare(l.Name, r.Name);
                });

                foreach (var node in nodes)
                    SortNodes(node.Children);
            }

            private readonly List<BranchTreeNode> _locals = new List<BranchTreeNode>();
            private readonly List<BranchTreeNode> _remotes = new List<BranchTreeNode>();
            private readonly List<string> _invalidExpandedNodes = new List<string>();
            private readonly HashSet<string> _expanded = new HashSet<string>();
        }
    }
}
