using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class DirectoryTree : ObservableObject
    {
        private static DirectoryTree _instance;
        public static DirectoryTree Instance => _instance ??= new DirectoryTree();

        public AvaloniaList<DirectoryTreeNode> Rows { get; } = [];

        public AvaloniaList<RecentRepo> RecentRepos { get; } = [];

        private bool _hasRecentRepos;
        public bool HasRecentRepos
        {
            get => _hasRecentRepos;
            private set => SetProperty(ref _hasRecentRepos, value);
        }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get => _isEmpty;
            private set => SetProperty(ref _isEmpty, value);
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            private set => SetProperty(ref _isScanning, value);
        }

        private bool _recentLoaded;

        private DirectoryTree() { }

        private void EnsureRecentLoaded()
        {
            if (_recentLoaded)
                return;
            _recentLoaded = true;
            LoadRecentRepos();
        }

        public void RecordRecentRepo(string path)
        {
            EnsureRecentLoaded();

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            var name = Path.GetFileName(path);

            Dispatcher.UIThread.Post(() =>
            {
                for (int i = RecentRepos.Count - 1; i >= 0; i--)
                {
                    if (RecentRepos[i].Path.Equals(path, StringComparison.Ordinal))
                    {
                        RecentRepos.RemoveAt(i);
                        break;
                    }
                }

                RecentRepos.Insert(0, new RecentRepo(path, name));

                while (RecentRepos.Count > 20)
                    RecentRepos.RemoveAt(RecentRepos.Count - 1);

                UpdateEmptyState();
                SaveRecentRepos();
            });
        }

        public async Task ScanAsync()
        {
            EnsureRecentLoaded();

            if (_isScanning)
                return;

            var cloneDir = Preferences.Instance.GitDefaultCloneDir;
            if (string.IsNullOrEmpty(cloneDir) || !Directory.Exists(cloneDir))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Rows.Clear();
                    UpdateEmptyState();
                });
                return;
            }

            _isScanning = true;

            try
            {
                var rootDir = new DirectoryInfo(cloneDir);
                var rootNode = new DirectoryTreeNode(rootDir.FullName, rootDir.Name, false);
                await BuildTreeAsync(rootNode, rootDir, 0);
                PruneEmptyFolders(rootNode);

                var rows = new List<DirectoryTreeNode>();
                foreach (var child in rootNode.Children)
                    MakeTreeRows(rows, child, 0);

                Dispatcher.UIThread.Post(() =>
                {
                    Rows.Clear();
                    Rows.AddRange(rows);
                    UpdateEmptyState();
                });
            }
            finally
            {
                _isScanning = false;
            }
        }

        public void ToggleNodeIsExpanded(DirectoryTreeNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            var depth = node.Depth;
            var idx = Rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subrows = new List<DirectoryTreeNode>();
                foreach (var child in node.Children)
                    MakeTreeRows(subrows, child, depth + 1);
                Rows.InsertRange(idx + 1, subrows);
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < Rows.Count; i++)
                {
                    if (Rows[i].Depth <= depth)
                        break;
                    removeCount++;
                }
                Rows.RemoveRange(idx + 1, removeCount);
            }
        }

        private void UpdateEmptyState()
        {
            HasRecentRepos = RecentRepos.Count > 0;
            IsEmpty = Rows.Count == 0 && !HasRecentRepos;
        }

        private void LoadRecentRepos()
        {
            foreach (var path in Preferences.Instance.RecentRepositories)
            {
                if (Directory.Exists(path))
                    RecentRepos.Add(new RecentRepo(path, Path.GetFileName(path)));
            }
            UpdateEmptyState();
        }

        private void SaveRecentRepos()
        {
            Preferences.Instance.RecentRepositories.Clear();
            foreach (var repo in RecentRepos)
                Preferences.Instance.RecentRepositories.Add(repo.Path);
            Preferences.Instance.Save();
        }

        private void MakeTreeRows(List<DirectoryTreeNode> rows, DirectoryTreeNode node, int depth)
        {
            node.Depth = depth;
            rows.Add(node);

            if (node.IsRepository || !node.IsExpanded)
                return;

            foreach (var child in node.Children)
                MakeTreeRows(rows, child, depth + 1);
        }

        private void PruneEmptyFolders(DirectoryTreeNode node)
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                if (!child.IsRepository)
                {
                    PruneEmptyFolders(child);
                    if (child.Children.Count == 0)
                        node.Children.RemoveAt(i);
                }
            }
        }

        private async Task BuildTreeAsync(DirectoryTreeNode node, DirectoryInfo dir, int depth)
        {
            if (depth > 5)
                return;

            var subdirs = dir.GetDirectories("*", new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
            });

            foreach (var subdir in subdirs)
            {
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                var gitDir = Path.Combine(subdir.FullName, ".git");
                var isRepo = Directory.Exists(gitDir) || File.Exists(gitDir);

                if (!isRepo)
                {
                    isRepo = await new Commands.IsBareRepository(subdir.FullName).GetResultAsync();
                }

                var childNode = new DirectoryTreeNode(subdir.FullName, subdir.Name, isRepo);
                node.Children.Add(childNode);

                if (!isRepo)
                {
                    await BuildTreeAsync(childNode, subdir, depth + 1);
                }
            }
        }
    }
}
