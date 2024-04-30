using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Collections;

namespace SourceGit.Models
{
    public enum BranchTreeNodeType
    {
        Remote,
        Folder,
        Branch,
        Tag,
    }

    public class BranchTreeNode
    {
        public string Name { get; set; }
        public BranchTreeNodeType Type { get; set; }
        public object Backend { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsFiltered { get; set; }
        public List<BranchTreeNode> Children { get; set; } = new List<BranchTreeNode>();
        public List<BranchTreeNode> ChildrenVisible { get; set; } = new List<BranchTreeNode>();

        public bool IsUpstreamTrackStatusVisible
        {
            get => IsBranch && !string.IsNullOrEmpty((Backend as Branch).UpstreamTrackStatus);
        }

        public string UpstreamTrackStatus
        {
            get => Type == BranchTreeNodeType.Branch ? (Backend as Branch).UpstreamTrackStatus : "";
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
        
        public bool IsTag
        {
            get => Type == BranchTreeNodeType.Tag;
        }

        public bool IsCurrent
        {
            get => IsBranch && (Backend as Branch).IsCurrent;
        }

        public class Builder
        {
            public List<BranchTreeNode> Locals => _locals;
            public List<BranchTreeNode> Remotes => _remotes;
            public List<BranchTreeNode> Tags => _tags;
            
            public List<BranchTreeNode> LocalsVisible => _localsVisible;
            public List<BranchTreeNode> RemotesVisible => _remotesVisible;
            public List<BranchTreeNode> TagsVisible => _tagsVisible;

            public void Run(List<Tag> tags)
            {
                foreach (var tag in tags)
                {
                    var isFiltered = _filters.Contains(tag.Name);
                    MakeTagNode(tag, _tags, "tag", isFiltered);
                }

                SortNodes(_tags);

                var filter = Filter.Create(_searchQuery);
                filter.Apply(_tags, _tagsVisible);
            }

            public void Run(List<Branch> branches, List<Remote> remotes)
            {
                foreach (var remote in remotes)
                {
                    var path = $"remote/{remote.Name}";
                    var node = new BranchTreeNode()
                    {
                        Name = remote.Name,
                        Type = BranchTreeNodeType.Remote,
                        Backend = remote,
                        IsExpanded = _expanded.Contains(path),
                    };

                    _maps.Add(path, node);
                    _remotes.Add(node);
                }

                foreach (var branch in branches)
                {
                    var isFiltered = _filters.Contains(branch.FullName);
                    if (branch.IsLocal)
                    {
                        MakeBranchNode(branch, _locals, "local", isFiltered);
                    }
                    else
                    {
                        var remote = _remotes.Find(x => x.Name == branch.Remote);
                        if (remote != null)
                            MakeBranchNode(branch, remote.Children, $"remote/{remote.Name}", isFiltered);
                    }
                }

                SortNodes(_locals);
                SortNodes(_remotes);

                var filter = Filter.Create(_searchQuery);
                filter.Apply(_locals, _localsVisible);
                filter.Apply(_remotes, _remotesVisible);
            }

            public void SetFilters(AvaloniaList<string> filters)
            {
                _filters.AddRange(filters);
            }

            public void SetSearchQuery(string searchQuery)
            {
                _searchQuery = searchQuery;
            }

            public void CollectBranchExpandedNodes(List<BranchTreeNode> nodes, bool isLocal)
            {
                CollectExpandedNodes(nodes, isLocal ? "local" : "remote");
            }

            public void CollectTagExpandedNodes(List<BranchTreeNode> nodes)
            {
                CollectExpandedNodes(nodes, "tag");
            }
            
            private void CollectExpandedNodes(List<BranchTreeNode> nodes, string prefix)
            {
                foreach (var node in nodes)
                {
                    var path = prefix + "/" + node.Name;
                    if (node.Type != BranchTreeNodeType.Branch
                        && node.Type != BranchTreeNodeType.Tag
                        && node.IsExpanded)
                        _expanded.Add(path);
                    CollectExpandedNodes(node.Children, path);
                }
            }

            private void MakeBranchNode(Branch branch, List<BranchTreeNode> roots, string prefix, bool isFiltered)
            {
                var subs = branch.Name.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (subs.Length == 1)
                {
                    var node = new BranchTreeNode()
                        {
                            Name = subs[0],
                            Type = BranchTreeNodeType.Branch,
                            Backend = branch,
                            IsExpanded = false,
                            IsFiltered = isFiltered,
                        };
                    roots.Add(node);
                    return;
                }

                BranchTreeNode lastFolder = null;
                string path = prefix;
                for (int i = 0; i < subs.Length - 1; i++)
                {
                    path = string.Concat(path, "/", subs[i]);
                    if (_maps.TryGetValue(path, out var value))
                    {
                        lastFolder = value;
                    }
                    else if (lastFolder == null)
                    {
                        lastFolder = new BranchTreeNode()
                        {
                            Name = subs[i],
                            Type = BranchTreeNodeType.Folder,
                            IsExpanded = branch.IsCurrent || _expanded.Contains(path),
                        };
                        roots.Add(lastFolder);
                        _maps.Add(path, lastFolder);
                    }
                    else
                    {
                        var folder = new BranchTreeNode()
                        {
                            Name = subs[i],
                            Type = BranchTreeNodeType.Folder,
                            IsExpanded = branch.IsCurrent || _expanded.Contains(path),
                        };
                        _maps.Add(path, folder);
                        lastFolder.Children.Add(folder);
                        lastFolder = folder;
                    }
                }

                var last = new BranchTreeNode()
                {
                    Name = subs[subs.Length - 1],
                    Type = BranchTreeNodeType.Branch,
                    Backend = branch,
                    IsExpanded = false,
                    IsFiltered = isFiltered,
                };
                lastFolder.Children.Add(last);
            }

            private void MakeTagNode(Tag tag, List<BranchTreeNode> roots, string prefix, bool isFiltered)
            {
                var subs = tag.Name.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (subs.Length == 1)
                {
                    var node = new BranchTreeNode()
                        {
                            Name = subs[0],
                            Type = BranchTreeNodeType.Tag,
                            Backend = tag,
                            IsExpanded = false,
                            IsFiltered = isFiltered,
                        };
                    roots.Add(node);
                    return;
                }

                BranchTreeNode lastFolder = null;
                string path = prefix;
                for (int i = 0; i < subs.Length - 1; i++)
                {
                    path = string.Concat(path, "/", subs[i]);
                    if (_maps.TryGetValue(path, out var value))
                    {
                        lastFolder = value;
                    }
                    else if (lastFolder == null)
                    {
                        lastFolder = new BranchTreeNode()
                        {
                            Name = subs[i],
                            Type = BranchTreeNodeType.Folder,
                            IsExpanded = _expanded.Contains(path),
                        };
                        roots.Add(lastFolder);
                        _maps.Add(path, lastFolder);
                    }
                    else
                    {
                        var folder = new BranchTreeNode()
                        {
                            Name = subs[i],
                            Type = BranchTreeNodeType.Folder,
                            IsExpanded = _expanded.Contains(path),
                        };
                        _maps.Add(path, folder);
                        lastFolder.Children.Add(folder);
                        lastFolder = folder;
                    }
                }

                var last = new BranchTreeNode()
                {
                    Name = subs[subs.Length - 1],
                    Type = BranchTreeNodeType.Branch,
                    Backend = tag,
                    IsExpanded = false,
                    IsFiltered = isFiltered,
                };
                lastFolder.Children.Add(last);
            }

            private void SortNodes(List<BranchTreeNode> nodes)
            {
                nodes.Sort((l, r) =>
                {
                    if (l.Type == r.Type)
                    {
                        return l.Name.CompareTo(r.Name);
                    }
                    else
                    {
                        return (int)(l.Type) - (int)(r.Type);
                    }
                });

                foreach (var node in nodes)
                    SortNodes(node.Children);
            }

            private readonly List<BranchTreeNode> _locals = new List<BranchTreeNode>();
            private readonly List<BranchTreeNode> _remotes = new List<BranchTreeNode>();
            private readonly List<BranchTreeNode> _tags = new List<BranchTreeNode>();
            private readonly List<BranchTreeNode> _localsVisible = new List<BranchTreeNode>();
            private readonly List<BranchTreeNode> _remotesVisible = new List<BranchTreeNode>();
            private readonly List<BranchTreeNode> _tagsVisible = new List<BranchTreeNode>();
            private readonly HashSet<string> _expanded = new HashSet<string>();
            private readonly List<string> _filters = new List<string>();
            private readonly Dictionary<string, BranchTreeNode> _maps = new Dictionary<string, BranchTreeNode>();
            private string _searchQuery;
        }
        
        public class Filter
        {
            public void Apply(List<BranchTreeNode> sourceNodes, List<BranchTreeNode> visibleNodes)
            {
                _applyQueryAction(sourceNodes, visibleNodes);
            }
            
            public List<BranchTreeNode> Apply(List<BranchTreeNode> sourceNodes)
            {
                var visibleNodes = new List<BranchTreeNode>(sourceNodes.Count);
                _applyQueryAction(sourceNodes, visibleNodes);
                return visibleNodes;
            }
            
            public static Filter Create(string query)
            {
                if (string.IsNullOrEmpty(query)
                    || string.IsNullOrWhiteSpace(query))
                {
                    return s_allPassFilter;
                }

                try
                {
                    var searchQueryRegex = new Regex(query, RegexOptions.IgnoreCase);
                    return new Filter(searchQueryRegex);
                }
                catch (RegexParseException e)
                {
                    // invalid regex, do nothing untill user type valid regex
                }
                
                return s_allPassFilter;
            }
            
            private Filter()
            {
                _applyQueryAction = AllNodesVisible;
            }
            
            private Filter(Regex searchQueryRegex)
            {
                _applyQueryAction = (source, visible) => 
                    BranchNameMatchesRegexNodesVisible(searchQueryRegex, source, visible);
            }
            
            private readonly Action<List<BranchTreeNode>, List<BranchTreeNode>> _applyQueryAction;
            
            private static readonly Filter s_allPassFilter = new();
            
            private static bool BranchNameMatchesRegexNodesVisible(Regex searchQueryRegex, List<BranchTreeNode> sourceNodes, List<BranchTreeNode> visibleNodes)
            {
                var isAnyBranchMatchesFilter = false;
                foreach (var node in sourceNodes)
                {
                    if (node.IsBranch
                        && node.Backend is Branch branch
                        && searchQueryRegex.IsMatch(branch.Name))
                    {
                        isAnyBranchMatchesFilter = true;
                        visibleNodes.Add(node);
                    }
                    else if (node.IsTag
                             && node.Backend is Tag tag
                             && searchQueryRegex.IsMatch(tag.Name))
                    {
                        isAnyBranchMatchesFilter = true;
                        visibleNodes.Add(node);
                    }
                    else
                    {
                        node.ChildrenVisible.Clear();
                        if (BranchNameMatchesRegexNodesVisible(searchQueryRegex, node.Children, node.ChildrenVisible))
                        {
                            isAnyBranchMatchesFilter = true;
                            visibleNodes.Add(node);
                        }
                    }
                }

                return isAnyBranchMatchesFilter;
            }
            
            private static void AllNodesVisible(List<BranchTreeNode> sourceNodes, List<BranchTreeNode> visibleNodes)
            {
                visibleNodes.AddRange(sourceNodes);
                foreach (var node in sourceNodes)
                {
                    node.ChildrenVisible.Clear();
                    AllNodesVisible(node.Children, node.ChildrenVisible);
                }
            }
        }
    }
}
