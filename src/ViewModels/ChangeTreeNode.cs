using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ChangeTreeNode : ObservableObject
    {
        public string FullPath { get; set; }
        public string DisplayName { get; set; }
        public int Depth { get; private set; } = 0;
        public Models.Change Change { get; set; } = null;
        public List<ChangeTreeNode> Children { get; set; } = new List<ChangeTreeNode>();

        public bool IsFolder
        {
            get => Change == null;
        }

        public bool ShowConflictMarker
        {
            get => Change is { IsConflicted: true };
        }

        public string ConflictMarker
        {
            get => Change?.ConflictMarker ?? string.Empty;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ChangeTreeNode(Models.Change c)
        {
            FullPath = c.Path;
            DisplayName = Path.GetFileName(c.Path);
            Change = c;
            IsExpanded = false;
        }

        public ChangeTreeNode(string path, bool isExpanded)
        {
            FullPath = path;
            DisplayName = Path.GetFileName(path);
            IsExpanded = isExpanded;
        }

        public static List<ChangeTreeNode> Build(IList<Models.Change> changes, HashSet<string> folded, Models.ChangeSortMode sortMode = Models.ChangeSortMode.Path, bool isUnstagedContext = false, bool compactFolders = false)
        {
            var nodes = new List<ChangeTreeNode>();
            var folders = new Dictionary<string, ChangeTreeNode>();

            foreach (var c in changes)
            {
                var sepIdx = c.Path.IndexOf('/');
                if (sepIdx == -1)
                {
                    nodes.Add(new ChangeTreeNode(c));
                }
                else
                {
                    ChangeTreeNode lastFolder = null;

                    while (sepIdx != -1)
                    {
                        var folder = c.Path.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new ChangeTreeNode(folder, !folded.Contains(folder));
                            folders.Add(folder, lastFolder);
                            InsertFolder(nodes, lastFolder);
                        }
                        else
                        {
                            var cur = new ChangeTreeNode(folder, !folded.Contains(folder));
                            folders.Add(folder, cur);
                            InsertFolder(lastFolder.Children, cur);
                            lastFolder = cur;
                        }

                        sepIdx = c.Path.IndexOf('/', sepIdx + 1);
                    }

                    lastFolder?.Children.Add(new ChangeTreeNode(c));
                }
            }

            if (compactFolders)
            {
                foreach (var node in nodes)
                    Compact(node);
            }

            Sort(nodes, sortMode, isUnstagedContext);

            folders.Clear();
            return nodes;
        }

        private static void InsertFolder(List<ChangeTreeNode> collection, ChangeTreeNode subFolder)
        {
            for (int i = 0; i < collection.Count; i++)
            {
                if (!collection[i].IsFolder)
                {
                    collection.Insert(i, subFolder);
                    return;
                }
            }

            collection.Add(subFolder);
        }

        private static void Compact(ChangeTreeNode node)
        {
            var childrenCount = node.Children.Count;
            if (childrenCount == 0)
                return;

            if (childrenCount > 1)
            {
                foreach (var c in node.Children)
                    Compact(c);
                return;
            }

            var child = node.Children[0];
            if (child.Change != null)
                return;

            node.FullPath = $"{node.FullPath}/{child.DisplayName}";
            node.DisplayName = $"{node.DisplayName} / {child.DisplayName}";
            node.IsExpanded = child.IsExpanded;
            node.Children = child.Children;
            Compact(node);
        }

        private static void Sort(List<ChangeTreeNode> nodes, Models.ChangeSortMode sortMode, bool isUnstagedContext, int depth = 0)
        {
            foreach (var node in nodes)
            {
                node.Depth = depth;
                if (node.IsFolder)
                    Sort(node.Children, sortMode, isUnstagedContext, depth + 1);
            }

            if (sortMode == Models.ChangeSortMode.Status)
            {
                nodes.Sort((l, r) =>
                {
                    // Sort folders first
                    if (l.IsFolder != r.IsFolder)
                        return l.IsFolder ? -1 : 1;

                    // If both are folders, sort by path
                    if (l.IsFolder && r.IsFolder)
                        return Models.NumericSort.Compare(l.DisplayName, r.DisplayName);

                    // For files, sort by status first
                    var leftPriority = Models.Change.GetStatusSortPriority(l.Change, isUnstagedContext);
                    var rightPriority = Models.Change.GetStatusSortPriority(r.Change, isUnstagedContext);
                    
                    // First sort by status priority
                    var statusComparison = leftPriority.CompareTo(rightPriority);
                    if (statusComparison != 0)
                        return statusComparison;

                    // If status priorities are equal, sort by path as secondary sort
                    return Models.NumericSort.Compare(l.DisplayName, r.DisplayName);
                });
            }
            else
            {
                nodes.Sort((l, r) =>
                {
                    if (l.IsFolder == r.IsFolder)
                        return Models.NumericSort.Compare(l.DisplayName, r.DisplayName);
                    return l.IsFolder ? -1 : 1;
                });
            }
        }

        private bool _isExpanded = true;
    }
}
