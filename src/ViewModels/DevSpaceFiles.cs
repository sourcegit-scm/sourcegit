using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceFiles : ObservableObject
    {
        public AvaloniaList<DevSpaceFileNode> VisibleItems { get; } = [];

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value ?? string.Empty))
                    RebuildVisibleItems();
            }
        }

        public DevSpaceFileNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (!SetProperty(ref _selectedNode, value))
                    return;

                _selectedPath = value?.RelativePath ?? string.Empty;
                if (value == null || value.IsDirectory)
                {
                    DetailContext = null;
                    return;
                }

                _ = LoadDetailAsync(value);
            }
        }

        public object DetailContext
        {
            get => _detailContext;
            private set => SetProperty(ref _detailContext, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public DevSpaceFiles(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            var refreshVersion = ++_refreshVersion;
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);

            var tracked = await new Commands.QueryRevisionFileNames(_workingDirectory, "HEAD")
                .GetResultAsync()
                .ConfigureAwait(false);
            var changes = await new Commands.QueryLocalChanges(_workingDirectory)
                .GetResultAsync()
                .ConfigureAwait(false);

            if (refreshVersion != _refreshVersion)
                return;

            var expanded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in _nodesByPath.Values)
            {
                if (node.IsDirectory && node.IsExpanded)
                    expanded.Add(node.RelativePath);
            }

            var paths = new HashSet<string>((tracked ?? []).Select(NormalizePath), StringComparer.Ordinal);
            foreach (var change in changes)
            {
                if (!string.IsNullOrEmpty(change.OriginalPath) && change.StateIsRename())
                    paths.Remove(NormalizePath(change.OriginalPath));

                if (!string.IsNullOrEmpty(change.Path))
                    paths.Add(NormalizePath(change.Path));
            }

            var built = BuildTree(paths, changes, expanded);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _roots = built.Roots;
                _nodesByPath = built.NodesByPath;
                RebuildVisibleItems();

                if (!string.IsNullOrEmpty(_selectedPath) && _nodesByPath.TryGetValue(_selectedPath, out var selected))
                    SelectedNode = selected;
                else
                    SelectedNode = null;

                IsLoading = false;
            });
        }

        public void ToggleExpanded(DevSpaceFileNode node)
        {
            if (node == null || !node.IsDirectory)
                return;

            node.IsExpanded = !node.IsExpanded;
            RebuildVisibleItems();
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public IReadOnlyList<string> GetSearchableFilePaths()
        {
            return _nodesByPath.Values
                .Where(x => !x.IsDirectory)
                .Select(x => x.RelativePath)
                .OrderBy(x => x, Comparer<string>.Create(Models.NumericSort.Compare))
                .ToArray();
        }

        public bool OpenFile(string relativePath)
        {
            var normalized = NormalizePath(relativePath);
            if (!_nodesByPath.TryGetValue(normalized, out var node) || node.IsDirectory)
                return false;

            Filter = string.Empty;

            var current = normalized;
            while (true)
            {
                var slash = current.LastIndexOf('/');
                if (slash <= 0)
                    break;

                current = current[..slash];
                if (_nodesByPath.TryGetValue(current, out var parent) && parent.IsDirectory)
                    parent.IsExpanded = true;
            }

            RebuildVisibleItems();
            SelectedNode = node;
            return true;
        }

        private async Task LoadDetailAsync(DevSpaceFileNode node)
        {
            if (node.Change != null)
            {
                var previous = DetailContext as DiffContext;
                await Dispatcher.UIThread.InvokeAsync(() =>
                    DetailContext = new DiffContext(_workingDirectory, new Models.DiffOption(node.Change), previous));
                return;
            }

            var absolutePath = Path.Combine(_workingDirectory, node.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var preview = await Task.Run(() => LoadWorkspaceFile(node.RelativePath, absolutePath)).ConfigureAwait(false);

            if (!string.Equals(_selectedPath, node.RelativePath, StringComparison.Ordinal))
                return;

            await Dispatcher.UIThread.InvokeAsync(() => DetailContext = preview);
        }

        private static DevSpaceWorkspaceFile LoadWorkspaceFile(string relativePath, string absolutePath)
        {
            try
            {
                var info = new FileInfo(absolutePath);
                if (!info.Exists)
                    return new DevSpaceWorkspaceFile(relativePath, string.Empty, "File no longer exists in the workspace.");
                if (info.Length > MaxPreviewBytes)
                    return new DevSpaceWorkspaceFile(relativePath, string.Empty, "File is too large to preview.");

                using (var stream = File.OpenRead(absolutePath))
                {
                    var sampleSize = (int)Math.Min(8192, stream.Length);
                    var sample = new byte[sampleSize];
                    var read = stream.Read(sample, 0, sample.Length);
                    for (var i = 0; i < read; i++)
                    {
                        if (sample[i] == 0)
                            return new DevSpaceWorkspaceFile(relativePath, string.Empty, "Binary file preview is not available here.");
                    }
                }

                return new DevSpaceWorkspaceFile(relativePath, File.ReadAllText(absolutePath));
            }
            catch (Exception ex)
            {
                return new DevSpaceWorkspaceFile(relativePath, string.Empty, ex.Message);
            }
        }

        private void RebuildVisibleItems()
        {
            VisibleItems.Clear();
            if (_roots.Count == 0)
                return;

            var hasFilter = !string.IsNullOrWhiteSpace(_filter);
            foreach (var root in _roots)
                AppendVisible(root, hasFilter);
        }

        private bool AppendVisible(DevSpaceFileNode node, bool hasFilter)
        {
            if (!hasFilter)
            {
                VisibleItems.Add(node);
                if (node.IsDirectory && node.IsExpanded)
                {
                    foreach (var child in node.Children)
                        AppendVisible(child, false);
                }

                return true;
            }

            var childMatches = new List<DevSpaceFileNode>();
            foreach (var child in node.Children)
            {
                if (MatchesFilter(child))
                    childMatches.Add(child);
            }

            var matches = PathMatches(node.RelativePath, _filter) || childMatches.Count > 0;
            if (!matches)
                return false;

            VisibleItems.Add(node);
            foreach (var child in childMatches)
                AppendVisible(child, true);
            return true;
        }

        private bool MatchesFilter(DevSpaceFileNode node)
        {
            if (PathMatches(node.RelativePath, _filter))
                return true;

            foreach (var child in node.Children)
            {
                if (MatchesFilter(child))
                    return true;
            }

            return false;
        }

        private static bool PathMatches(string path, string filter)
        {
            return path.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }

        private static (List<DevSpaceFileNode> Roots, Dictionary<string, DevSpaceFileNode> NodesByPath) BuildTree(
            HashSet<string> paths,
            List<Models.Change> changes,
            HashSet<string> expanded)
        {
            var roots = new List<DevSpaceFileNode>();
            var nodes = new Dictionary<string, DevSpaceFileNode>(StringComparer.Ordinal);

            foreach (var rawPath in paths)
            {
                var path = NormalizePath(rawPath);
                if (string.IsNullOrWhiteSpace(path) || path.StartsWith(".git/", StringComparison.Ordinal) || path == ".git")
                    continue;

                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var currentPath = string.Empty;
                DevSpaceFileNode parent = null;
                for (var i = 0; i < segments.Length; i++)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? segments[i] : $"{currentPath}/{segments[i]}";
                    if (!nodes.TryGetValue(currentPath, out var node))
                    {
                        var isDirectory = i < segments.Length - 1;
                        node = new DevSpaceFileNode(segments[i], currentPath, isDirectory, i)
                        {
                            IsExpanded = isDirectory && expanded.Contains(currentPath),
                        };
                        nodes.Add(currentPath, node);

                        if (parent == null)
                            roots.Add(node);
                        else
                            parent.Children.Add(node);
                    }

                    parent = node;
                }
            }

            foreach (var change in changes)
            {
                var path = NormalizePath(change.Path);
                if (nodes.TryGetValue(path, out var node))
                    node.Change = change;
            }

            SortNodes(roots);
            return (roots, nodes);
        }

        private static void SortNodes(List<DevSpaceFileNode> nodes)
        {
            nodes.Sort((left, right) =>
            {
                if (left.IsDirectory != right.IsDirectory)
                    return left.IsDirectory ? -1 : 1;
                return Models.NumericSort.Compare(left.Name, right.Name);
            });

            foreach (var node in nodes)
            {
                if (node.Children.Count > 0)
                    SortNodes(node.Children);
            }
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private const long MaxPreviewBytes = 1024 * 1024;
        private readonly string _workingDirectory;
        private List<DevSpaceFileNode> _roots = [];
        private Dictionary<string, DevSpaceFileNode> _nodesByPath = new(StringComparer.Ordinal);
        private string _filter = string.Empty;
        private string _selectedPath = string.Empty;
        private DevSpaceFileNode _selectedNode;
        private object _detailContext;
        private bool _isLoading;
        private int _refreshVersion;
    }

    internal static class DevSpaceChangeExtensions
    {
        public static bool StateIsRename(this Models.Change change)
        {
            return change.Index == Models.ChangeState.Renamed || change.WorkTree == Models.ChangeState.Renamed;
        }
    }
}
