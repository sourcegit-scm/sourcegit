using System.Collections;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class TagToolTip
    {
        public string Name { get; private set; }
        public bool IsAnnotated { get; private set; }
        public Models.User Creator { get; private set; }
        public string CreatorDateStr { get; private set; }
        public string Message { get; private set; }

        public TagToolTip(Models.Tag t)
        {
            Name = t.Name;
            IsAnnotated = t.IsAnnotated;
            Creator = t.Creator;
            CreatorDateStr = t.CreatorDateStr;
            Message = t.Message;
        }
    }

    public class TagTreeNode : ObservableObject
    {
        public string FullPath { get; private set; }
        public int Depth { get; private set; } = 0;
        public Models.Tag Tag { get; private set; } = null;
        public TagToolTip ToolTip { get; private set; } = null;
        public List<TagTreeNode> Children { get; private set; } = [];
        public int Counter { get; set; } = 0;

        public bool IsFolder
        {
            get => Tag == null;
        }

        public bool IsSelected
        {
            get;
            set;
        }

        public Models.FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        public CornerRadius CornerRadius
        {
            get => _cornerRadius;
            set => SetProperty(ref _cornerRadius, value);
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
            ToolTip = new TagToolTip(t);
            IsExpanded = false;
        }

        public TagTreeNode(string path, bool isExpanded, int depth)
        {
            FullPath = path;
            Depth = depth;
            IsExpanded = isExpanded;
            Counter = 1;
        }

        public void UpdateFilterMode(Dictionary<string, Models.FilterMode> filters)
        {
            if (Tag == null)
            {
                foreach (var child in Children)
                    child.UpdateFilterMode(filters);
            }
            else
            {
                FilterMode = filters.GetValueOrDefault(FullPath, Models.FilterMode.None);
            }
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

        private Models.FilterMode _filterMode = Models.FilterMode.None;
        private CornerRadius _cornerRadius = new CornerRadius(4);
        private bool _isExpanded = true;
    }

    public class TagListItem : ObservableObject
    {
        public Models.Tag Tag
        {
            get;
            set;
        }

        public bool IsSelected
        {
            get;
            set;
        }

        public Models.FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        public TagToolTip ToolTip
        {
            get;
            set;
        }

        public CornerRadius CornerRadius
        {
            get => _cornerRadius;
            set => SetProperty(ref _cornerRadius, value);
        }

        private Models.FilterMode _filterMode = Models.FilterMode.None;
        private CornerRadius _cornerRadius = new CornerRadius(4);
    }

    public class TagCollectionAsList
    {
        public List<TagListItem> TagItems
        {
            get;
            set;
        } = [];

        public TagCollectionAsList(List<Models.Tag> tags)
        {
            foreach (var tag in tags)
                TagItems.Add(new TagListItem() { Tag = tag, ToolTip = new TagToolTip(tag) });
        }

        public void ClearSelection()
        {
            foreach (var item in TagItems)
            {
                item.IsSelected = false;
                item.CornerRadius = new CornerRadius(4);
            }
        }

        public void UpdateSelection(IList selectedItems)
        {
            var set = new HashSet<string>();
            foreach (var item in selectedItems)
            {
                if (item is TagListItem tagItem)
                    set.Add(tagItem.Tag.Name);
            }

            TagListItem last = null;
            foreach (var item in TagItems)
            {
                item.IsSelected = set.Contains(item.Tag.Name);
                if (item.IsSelected)
                {
                    if (last is { IsSelected: true })
                    {
                        last.CornerRadius = new CornerRadius(last.CornerRadius.TopLeft, 0);
                        item.CornerRadius = new CornerRadius(0, 4);
                    }
                    else
                    {
                        item.CornerRadius = new CornerRadius(4);
                    }
                }
                else
                {
                    item.CornerRadius = new CornerRadius(4);
                }

                last = item;
            }
        }
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

        public void ClearSelection()
        {
            foreach (var node in Tree)
                ClearSelectionRecursively(node);
        }

        public void UpdateSelection(IList selectedItems)
        {
            var set = new HashSet<string>();
            foreach (var item in selectedItems)
            {
                if (item is TagTreeNode node)
                    set.Add(node.FullPath);
            }

            TagTreeNode last = null;
            foreach (var row in Rows)
            {
                row.IsSelected = set.Contains(row.FullPath);
                if (row.IsSelected)
                {
                    if (last is { IsSelected: true })
                    {
                        last.CornerRadius = new CornerRadius(last.CornerRadius.TopLeft, 0);
                        row.CornerRadius = new CornerRadius(0, 4);
                    }
                    else
                    {
                        row.CornerRadius = new CornerRadius(4);
                    }
                }
                else
                {
                    row.CornerRadius = new CornerRadius(4);
                }

                last = row;
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

        private static void ClearSelectionRecursively(TagTreeNode node)
        {
            if (node.IsSelected)
            {
                node.IsSelected = false;
                node.CornerRadius = new CornerRadius(4);
            }

            foreach (var child in node.Children)
                ClearSelectionRecursively(child);
        }
    }
}
