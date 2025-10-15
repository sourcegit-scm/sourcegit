using System;
using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BranchTreeNode : ObservableObject
    {
        public string Name { get; private set; } = string.Empty;
        public string Path { get; private set; } = string.Empty;
        public object Backend { get; private set; } = null;
        public ulong TimeToSort { get; private set; } = 0;
        public int Depth { get; set; } = 0;
        public bool IsSelected { get; set; } = false;
        public List<BranchTreeNode> Children { get; private set; } = new List<BranchTreeNode>();
        public int Counter { get; set; } = 0;

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

        public bool IsCurrent
        {
            get => Backend is Models.Branch { IsCurrent: true };
        }

        public bool ShowUpstreamGoneTip
        {
            get => Backend is Models.Branch { IsUpstreamGone: true };
        }

        public string BranchesCount
        {
            get => Counter > 0 ? $"({Counter})" : string.Empty;
        }

        private Models.FilterMode _filterMode = Models.FilterMode.None;
        private bool _isExpanded = false;
        private CornerRadius _cornerRadius = new CornerRadius(4);

        public class Builder
        {
            public List<BranchTreeNode> Locals { get; } = [];
            public List<BranchTreeNode> Remotes { get; } = [];
            public List<string> InvalidExpandedNodes { get; } = [];

            public Builder(Models.BranchSortMode localSortMode, Models.BranchSortMode remoteSortMode)
            {
                _localSortMode = localSortMode;
                _remoteSortMode = remoteSortMode;
            }

            public void SetExpandedNodes(List<string> expanded)
            {
                foreach (var node in expanded)
                    _expanded.Add(node);
            }

            public void Run(List<Models.Branch> branches, List<Models.Remote> remotes, bool bForceExpanded)
            {
                var folders = new Dictionary<string, BranchTreeNode>();

                var fakeRemoteTime = (ulong)remotes.Count;
                foreach (var remote in remotes)
                {
                    var path = $"refs/remotes/{remote.Name}";
                    var node = new BranchTreeNode()
                    {
                        Name = remote.Name,
                        Path = path,
                        Backend = remote,
                        IsExpanded = bForceExpanded || _expanded.Contains(path),
                        TimeToSort = fakeRemoteTime,
                    };

                    fakeRemoteTime--;
                    folders.Add(path, node);
                    Remotes.Add(node);
                }

                foreach (var branch in branches)
                {
                    if (branch.IsLocal)
                    {
                        MakeBranchNode(branch, Locals, folders, "refs/heads", bForceExpanded);
                        continue;
                    }

                    var rk = $"refs/remotes/{branch.Remote}";
                    if (folders.TryGetValue(rk, out var remote))
                    {
                        remote.Counter++;
                        MakeBranchNode(branch, remote.Children, folders, rk, bForceExpanded);
                    }
                }

                foreach (var path in _expanded)
                {
                    if (!folders.ContainsKey(path))
                        InvalidExpandedNodes.Add(path);
                }

                folders.Clear();

                if (_localSortMode == Models.BranchSortMode.Name)
                    SortNodesByName(Locals);
                else
                    SortNodesByTime(Locals);

                if (_remoteSortMode == Models.BranchSortMode.Name)
                    SortNodesByName(Remotes);
                else
                    SortNodesByTime(Remotes);
            }

            private void MakeBranchNode(Models.Branch branch, List<BranchTreeNode> roots, Dictionary<string, BranchTreeNode> folders, string prefix, bool bForceExpanded)
            {
                var time = branch.CommitterDate;
                var fullpath = $"{prefix}/{branch.Name}";
                var sepIdx = branch.Name.IndexOf('/');
                if (sepIdx == -1 || branch.IsDetachedHead)
                {
                    roots.Add(new BranchTreeNode()
                    {
                        Name = branch.Name,
                        Path = fullpath,
                        Backend = branch,
                        IsExpanded = false,
                        TimeToSort = time,
                    });
                    return;
                }

                BranchTreeNode lastFolder = null;
                var start = 0;

                while (sepIdx != -1)
                {
                    var folder = string.Concat(prefix, "/", branch.Name.Substring(0, sepIdx));
                    var name = branch.Name.Substring(start, sepIdx - start);
                    if (folders.TryGetValue(folder, out var val))
                    {
                        lastFolder = val;
                        lastFolder.Counter++;
                        lastFolder.TimeToSort = Math.Max(lastFolder.TimeToSort, time);
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
                            TimeToSort = time,
                            Counter = 1,
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
                            TimeToSort = time,
                            Counter = 1,
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
                    TimeToSort = time,
                });
            }

            private void SortNodesByName(List<BranchTreeNode> nodes)
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
                    SortNodesByName(node.Children);
            }

            private void SortNodesByTime(List<BranchTreeNode> nodes)
            {
                nodes.Sort((l, r) =>
                {
                    if (l.Backend is Models.Branch { IsDetachedHead: true })
                        return -1;

                    if (l.Backend is Models.Branch)
                    {
                        if (r.Backend is Models.Branch)
                            return r.TimeToSort == l.TimeToSort ? Models.NumericSort.Compare(l.Name, r.Name) : r.TimeToSort.CompareTo(l.TimeToSort);
                        return 1;
                    }

                    if (r.Backend is Models.Branch)
                        return -1;

                    if (r.TimeToSort == l.TimeToSort)
                        return Models.NumericSort.Compare(l.Name, r.Name);

                    return r.TimeToSort.CompareTo(l.TimeToSort);
                });

                foreach (var node in nodes)
                    SortNodesByTime(node.Children);
            }

            private readonly Models.BranchSortMode _localSortMode;
            private readonly Models.BranchSortMode _remoteSortMode;
            private readonly HashSet<string> _expanded = new HashSet<string>();
        }
    }
}
