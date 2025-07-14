using System.Collections.Generic;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SubmoduleTreeNode : ObservableObject
    {
        public string FullPath { get; private set; } = string.Empty;
        public int Depth { get; private set; } = 0;
        public Models.Submodule Module { get; private set; } = null;
        public List<SubmoduleTreeNode> Children { get; private set; } = [];
        public int Counter = 0;

        public bool IsFolder
        {
            get => Module == null;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public string ChildCounter
        {
            get => Counter > 0 ? $"({Counter})" : string.Empty;
        }

        public bool IsDirty
        {
            get => Module?.IsDirty ?? false;
        }

        public SubmoduleTreeNode(Models.Submodule module, int depth)
        {
            FullPath = module.Path;
            Depth = depth;
            Module = module;
            IsExpanded = false;
        }

        public SubmoduleTreeNode(string path, int depth, bool isExpanded)
        {
            FullPath = path;
            Depth = depth;
            IsExpanded = isExpanded;
            Counter = 1;
        }

        public static List<SubmoduleTreeNode> Build(IList<Models.Submodule> submodules, HashSet<string> expanded)
        {
            var nodes = new List<SubmoduleTreeNode>();
            var folders = new Dictionary<string, SubmoduleTreeNode>();

            foreach (var module in submodules)
            {
                var sepIdx = module.Path.IndexOf('/');
                if (sepIdx == -1)
                {
                    nodes.Add(new SubmoduleTreeNode(module, 0));
                }
                else
                {
                    SubmoduleTreeNode lastFolder = null;
                    int depth = 0;

                    while (sepIdx != -1)
                    {
                        var folder = module.Path.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                            lastFolder.Counter++;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new SubmoduleTreeNode(folder, depth, expanded.Contains(folder));
                            folders.Add(folder, lastFolder);
                            InsertFolder(nodes, lastFolder);
                        }
                        else
                        {
                            var cur = new SubmoduleTreeNode(folder, depth, expanded.Contains(folder));
                            folders.Add(folder, cur);
                            InsertFolder(lastFolder.Children, cur);
                            lastFolder = cur;
                        }

                        depth++;
                        sepIdx = module.Path.IndexOf('/', sepIdx + 1);
                    }

                    lastFolder?.Children.Add(new SubmoduleTreeNode(module, depth));
                }
            }

            folders.Clear();
            return nodes;
        }

        private static void InsertFolder(List<SubmoduleTreeNode> collection, SubmoduleTreeNode subFolder)
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

        private bool _isExpanded = false;
    }

    public class SubmoduleCollectionAsTree
    {
        public List<SubmoduleTreeNode> Tree
        {
            get;
            set;
        } = [];

        public AvaloniaList<SubmoduleTreeNode> Rows
        {
            get;
            set;
        } = [];

        public static SubmoduleCollectionAsTree Build(List<Models.Submodule> submodules, SubmoduleCollectionAsTree old)
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

            var collection = new SubmoduleCollectionAsTree();
            collection.Tree = SubmoduleTreeNode.Build(submodules, oldExpanded);

            var rows = new List<SubmoduleTreeNode>();
            MakeTreeRows(rows, collection.Tree);
            collection.Rows.AddRange(rows);

            return collection;
        }

        public void ToggleExpand(SubmoduleTreeNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            var rows = Rows;
            var depth = node.Depth;
            var idx = rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subrows = new List<SubmoduleTreeNode>();
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

        private static void MakeTreeRows(List<SubmoduleTreeNode> rows, List<SubmoduleTreeNode> nodes)
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

    public class SubmoduleCollectionAsList
    {
        public List<Models.Submodule> Submodules
        {
            get;
            set;
        } = [];
    }
}
