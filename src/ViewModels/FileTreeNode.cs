using System;
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class FileTreeNode : ObservableObject
    {
        public string FullPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; } = false;
        public object Backend { get; set; } = null;
        public List<FileTreeNode> Children { get; set; } = new List<FileTreeNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public static List<FileTreeNode> Build(List<Models.Change> changes, bool expanded)
        {
            var nodes = new List<FileTreeNode>();
            var folders = new Dictionary<string, FileTreeNode>();

            foreach (var c in changes)
            {
                var sepIdx = c.Path.IndexOf('/', StringComparison.Ordinal);
                if (sepIdx == -1)
                {
                    nodes.Add(new FileTreeNode()
                    {
                        FullPath = c.Path,
                        Backend = c,
                        IsFolder = false,
                        IsExpanded = false
                    });
                }
                else
                {
                    FileTreeNode lastFolder = null;
                    var start = 0;

                    while (sepIdx != -1)
                    {
                        var folder = c.Path.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new FileTreeNode()
                            {
                                FullPath = folder,
                                Backend = null,
                                IsFolder = true,
                                IsExpanded = expanded
                            };
                            nodes.Add(lastFolder);
                            folders.Add(folder, lastFolder);
                        }
                        else
                        {
                            var cur = new FileTreeNode()
                            {
                                FullPath = folder,
                                Backend = null,
                                IsFolder = true,
                                IsExpanded = expanded
                            };
                            folders.Add(folder, cur);
                            lastFolder.Children.Add(cur);
                            lastFolder = cur;
                        }

                        start = sepIdx + 1;
                        sepIdx = c.Path.IndexOf('/', start);
                    }

                    lastFolder.Children.Add(new FileTreeNode()
                    {
                        FullPath = c.Path,
                        Backend = c,
                        IsFolder = false,
                        IsExpanded = false
                    });
                }
            }

            folders.Clear();
            Sort(nodes);
            return nodes;
        }

        public static List<FileTreeNode> Build(List<Models.Object> files, bool expanded)
        {
            var nodes = new List<FileTreeNode>();
            var folders = new Dictionary<string, FileTreeNode>();

            foreach (var f in files)
            {
                var sepIdx = f.Path.IndexOf('/', StringComparison.Ordinal);
                if (sepIdx == -1)
                {
                    nodes.Add(new FileTreeNode()
                    {
                        FullPath = f.Path,
                        Backend = f,
                        IsFolder = false,
                        IsExpanded = false
                    });
                }
                else
                {
                    FileTreeNode lastFolder = null;
                    var start = 0;

                    while (sepIdx != -1)
                    {
                        var folder = f.Path.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new FileTreeNode()
                            {
                                FullPath = folder,
                                Backend = null,
                                IsFolder = true,
                                IsExpanded = expanded
                            };
                            nodes.Add(lastFolder);
                            folders.Add(folder, lastFolder);
                        }
                        else
                        {
                            var cur = new FileTreeNode()
                            {
                                FullPath = folder,
                                Backend = null,
                                IsFolder = true,
                                IsExpanded = expanded
                            };
                            folders.Add(folder, cur);
                            lastFolder.Children.Add(cur);
                            lastFolder = cur;
                        }

                        start = sepIdx + 1;
                        sepIdx = f.Path.IndexOf('/', start);
                    }

                    lastFolder.Children.Add(new FileTreeNode()
                    {
                        FullPath = f.Path,
                        Backend = f,
                        IsFolder = false,
                        IsExpanded = false
                    });
                }
            }

            folders.Clear();
            Sort(nodes);
            return nodes;
        }

        public static FileTreeNode SelectByPath(List<FileTreeNode> nodes, string path)
        {
            foreach (var node in nodes)
            {
                if (node.FullPath == path)
                    return node;

                if (node.IsFolder && path.StartsWith(node.FullPath + "/", StringComparison.Ordinal))
                {
                    var foundInChildren = SelectByPath(node.Children, path);
                    if (foundInChildren != null)
                    {
                        node.IsExpanded = true;
                    }
                    return foundInChildren;
                }
            }

            return null;
        }

        private static void Sort(List<FileTreeNode> nodes)
        {
            nodes.Sort((l, r) =>
            {
                if (l.IsFolder == r.IsFolder)
                {
                    return l.FullPath.CompareTo(r.FullPath);
                }
                else
                {
                    return l.IsFolder ? -1 : 1;
                }
            });

            foreach (var node in nodes)
            {
                if (node.Children.Count > 1)
                    Sort(node.Children);
            }
        }

        private bool _isExpanded = true;
    }
}
