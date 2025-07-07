using System.Collections.Generic;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class TagTreeNodeToolTip
    {
        public string Name { get; private set; }
        public bool IsAnnotated { get; private set; }
        public string Message { get; private set; }

        public TagTreeNodeToolTip(Models.Tag t)
        {
            Name = t.Name;
            IsAnnotated = t.IsAnnotated;
            Message = t.Message;
        }
    }

    public class TagTreeNode : ObservableObject
    {
        public string FullPath { get; private set; }
        public int Depth { get; private set; } = 0;
        public Models.Tag Tag { get; private set; } = null;
        public TagTreeNodeToolTip ToolTip { get; private set; } = null;
        public List<TagTreeNode> Children { get; private set; } = [];
        public int Counter { get; set; } = 0;

        public bool IsFolder
        {
            get => Tag == null;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public string TagsCount
        {
            get => Counter > 0 ? $"({Counter})" : string.Empty;
        }

        public TagTreeNode(Models.Tag t, int depth)
        {
            FullPath = t.Name;
            Depth = depth;
            Tag = t;
            ToolTip = new TagTreeNodeToolTip(t);
            IsExpanded = false;
        }

        public TagTreeNode(string path, bool isExpanded, int depth)
        {
            FullPath = path;
            Depth = depth;
            IsExpanded = isExpanded;
            Counter = 1;
        }

        public static List<TagTreeNode> Build(List<Models.Tag> tags, HashSet<string> expanded)
        {
            var nodes = new List<TagTreeNode>();
            var folders = new Dictionary<string, TagTreeNode>();

            foreach (var tag in tags)
            {
                var sepIdx = tag.Name.IndexOf('/');
                if (sepIdx == -1)
                {
                    nodes.Add(new TagTreeNode(tag, 0));
                }
                else
                {
                    TagTreeNode lastFolder = null;
                    int depth = 0;

                    while (sepIdx != -1)
                    {
                        var folder = tag.Name.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                            lastFolder.Counter++;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new TagTreeNode(folder, expanded.Contains(folder), depth);
                            folders.Add(folder, lastFolder);
                            InsertFolder(nodes, lastFolder);
                        }
                        else
                        {
                            var cur = new TagTreeNode(folder, expanded.Contains(folder), depth);
                            folders.Add(folder, cur);
                            InsertFolder(lastFolder.Children, cur);
                            lastFolder = cur;
                        }

                        depth++;
                        sepIdx = tag.Name.IndexOf('/', sepIdx + 1);
                    }

                    lastFolder?.Children.Add(new TagTreeNode(tag, depth));
                }
            }

            folders.Clear();
            return nodes;
        }

        private static void InsertFolder(List<TagTreeNode> collection, TagTreeNode subFolder)
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

        private bool _isExpanded = true;
    }

    public class TagCollectionAsList
    {
        public List<Models.Tag> Tags
        {
            get;
            set;
        } = [];
    }

    public class TagCollectionAsTree
    {
        public List<TagTreeNode> Tree
        {
            get;
            set;
        } = [];

        public AvaloniaList<TagTreeNode> Rows
        {
            get;
            set;
        } = [];

        public static TagCollectionAsTree Build(List<Models.Tag> tags, TagCollectionAsTree old)
        {
            var oldExpanded = new HashSet<string>();
            if (old != null)
            {
                foreach (var row in old.Rows)
                {
                    if (row.IsFolder && row.IsExpanded)
                        oldExpanded.Add(row.FullPath);
                }
            }

            var collection = new TagCollectionAsTree();
            collection.Tree = TagTreeNode.Build(tags, oldExpanded);

            var rows = new List<TagTreeNode>();
            MakeTreeRows(rows, collection.Tree);
            collection.Rows.AddRange(rows);

            return collection;
        }

        public void ToggleExpand(TagTreeNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            var rows = Rows;
            var depth = node.Depth;
            var idx = rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subrows = new List<TagTreeNode>();
                MakeTreeRows(subrows, node.Children);
                rows.InsertRange(idx + 1, subrows);
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }
                rows.RemoveRange(idx + 1, removeCount);
            }
        }

        private static void MakeTreeRows(List<TagTreeNode> rows, List<TagTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                rows.Add(node);

                if (!node.IsExpanded || !node.IsFolder)
                    continue;

                MakeTreeRows(rows, node.Children);
            }
        }
    }
}
