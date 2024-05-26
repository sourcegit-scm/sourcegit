using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public enum BranchTreeNodeType
    {
        DetachedHead,
        Remote,
        Folder,
        Branch,
    }

    public class BranchTreeNode : ObservableObject
    {
        public const double DEFAULT_CORNER = 4.0;

        public string Name { get; set; }
        public BranchTreeNodeType Type { get; set; }
        public object Backend { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsFiltered { get; set; }
        public List<BranchTreeNode> Children { get; set; } = new List<BranchTreeNode>();

        public bool IsUpstreamTrackStatusVisible
        {
            get => IsBranch && !string.IsNullOrEmpty((Backend as Models.Branch).UpstreamTrackStatus);
        }

        public string UpstreamTrackStatus
        {
            get => Type == BranchTreeNodeType.Branch ? (Backend as Models.Branch).UpstreamTrackStatus : "";
        }

        public bool IsRemote
        {
            get => Type == BranchTreeNodeType.Remote;
        }

        public bool IsFolder
        {
            get => Type == BranchTreeNodeType.Folder;
        }

        public bool IsBranch
        {
            get => Type == BranchTreeNodeType.Branch;
        }
        
        public bool IsDetachedHead
        {
            get => Type == BranchTreeNodeType.DetachedHead;
        }

        public bool IsCurrent
        {
            get => IsBranch && (Backend as Models.Branch).IsCurrent;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public CornerRadius CornerRadius
        {
            get => _cornerRadius;
            set => SetProperty(ref _cornerRadius, value);
        }

        public void UpdateCornerRadius(ref BranchTreeNode prev)
        {
            if (_isSelected && prev != null && prev.IsSelected)
            {
                var prevTop = prev.CornerRadius.TopLeft;
                prev.CornerRadius = new CornerRadius(prevTop, 0);
                CornerRadius = new CornerRadius(0, DEFAULT_CORNER);
            }
            else if (CornerRadius.TopLeft != DEFAULT_CORNER ||
                CornerRadius.BottomLeft != DEFAULT_CORNER)
            {
                CornerRadius = new CornerRadius(DEFAULT_CORNER);
            }

            prev = this;

            if (!IsBranch && IsExpanded)
            {
                foreach (var child in Children)
                    child.UpdateCornerRadius(ref prev);
            }
        }
        private bool _isSelected = false;
        private CornerRadius _cornerRadius = new CornerRadius(DEFAULT_CORNER);

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
                        Type = BranchTreeNodeType.Remote,
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
                    var path = prefix + "/" + node.Name;
                    if (node.Type != BranchTreeNodeType.Branch && node.IsExpanded)
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
                        Type = BranchTreeNodeType.Branch,
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
                    }
                    else if (lastFolder == null)
                    {
                        lastFolder = new BranchTreeNode()
                        {
                            Name = name,
                            Type = BranchTreeNodeType.Folder,
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
                            Type = BranchTreeNodeType.Folder,
                            IsExpanded = bForceExpanded || branch.IsCurrent || _expanded.Contains(folder),
                        };
                        lastFolder.Children.Add(cur);
                        folders.Add(folder, cur);
                        lastFolder = cur;
                    }

                    start = sepIdx + 1;
                    sepIdx = branch.Name.IndexOf('/', start);
                }
                
                lastFolder.Children.Add(new BranchTreeNode()
                {
                    Name = Path.GetFileName(branch.Name),
                    Type = branch.IsHead ? BranchTreeNodeType.DetachedHead : BranchTreeNodeType.Branch,
                    Backend = branch,
                    IsExpanded = false,
                    IsFiltered = isFiltered,
                });
            }

            private void SortNodes(List<BranchTreeNode> nodes)
            {
                nodes.Sort((l, r) =>
                {
                    if (l.Type == BranchTreeNodeType.DetachedHead)
                    {
                        return -1;
                    }
                    if (l.Type == r.Type)
                    {
                        return l.Name.CompareTo(r.Name);
                    }
                   
                    return (int)l.Type - (int)r.Type;
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
