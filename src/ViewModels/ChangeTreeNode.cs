using System;
using System.Collections.Generic;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ChangeTreeNode : ObservableObject
    {
        public string FullPath { get; set; } = string.Empty;
        public int Depth { get; private set; } = 0;
        public Models.Change Change { get; set; } = null;
        public List<ChangeTreeNode> Children { get; set; } = new List<ChangeTreeNode>();

        public bool IsFolder
        {
            get => Change == null;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ChangeTreeNode(Models.Change c, int depth)
        {
            FullPath = c.Path;
            Depth = depth;
            Change = c;
            IsExpanded = false;
        }

        public ChangeTreeNode(string path, bool isExpanded, int depth)
        {
            FullPath = path;
            Depth = depth;
            IsExpanded = isExpanded;
        }

        public static List<ChangeTreeNode> Build(IList<Models.Change> changes, HashSet<string> folded)
        {
            var nodes = new List<ChangeTreeNode>();
            var folders = new Dictionary<string, ChangeTreeNode>();

            foreach (var c in changes)
            {
                var sepIdx = c.Path.IndexOf('/', StringComparison.Ordinal);
                if (sepIdx == -1)
                {
                    nodes.Add(new ChangeTreeNode(c, 0));
                }
                else
                {
                    ChangeTreeNode lastFolder = null;
                    var start = 0;
                    var depth = 0;

                    while (sepIdx != -1)
                    {
                        var folder = c.Path.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new ChangeTreeNode(folder, !folded.Contains(folder), depth);
                            folders.Add(folder, lastFolder);
                            InsertFolder(nodes, lastFolder);
                        }
                        else
                        {
                            var cur = new ChangeTreeNode(folder, !folded.Contains(folder), depth);
                            folders.Add(folder, cur);
                            InsertFolder(lastFolder.Children, cur);
                            lastFolder = cur;
                        }

                        start = sepIdx + 1;
                        depth++;
                        sepIdx = c.Path.IndexOf('/', start);
                    }

                    lastFolder.Children.Add(new ChangeTreeNode(c, depth));
                }
            }

            Sort(nodes);

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

        private static void Sort(List<ChangeTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsFolder)
                    Sort(node.Children);
            }

            nodes.Sort((l, r) =>
            {
                if (l.IsFolder)
                    return r.IsFolder ? string.Compare(l.FullPath, r.FullPath, StringComparison.Ordinal) : -1;
                return r.IsFolder ? 1 : string.Compare(l.FullPath, r.FullPath, StringComparison.Ordinal);
            });
        }

        private bool _isExpanded = true;
    }

    public class ChangeCollectionAsTree
    {
        public List<ChangeTreeNode> Tree { get; set; } = new List<ChangeTreeNode>();
        public AvaloniaList<ChangeTreeNode> Rows { get; set; } = new AvaloniaList<ChangeTreeNode>();
    }

    public class ChangeCollectionAsGrid
    {
        public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    }

    public class ChangeCollectionAsList
    {
        public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    }
}
