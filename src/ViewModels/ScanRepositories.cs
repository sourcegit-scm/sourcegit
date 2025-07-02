﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public class ScanRepositories : Popup
    {
        public List<Models.ScanDir> ScanDirs
        {
            get;
        }

        [Required(ErrorMessage = "Scan directory is required!!!")]
        public Models.ScanDir Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value, true);
        }

        public ScanRepositories()
        {
            ScanDirs = new List<Models.ScanDir>();

            var workspace = Preferences.Instance.GetActiveWorkspace();
            if (!string.IsNullOrEmpty(workspace.DefaultCloneDir))
                ScanDirs.Add(new Models.ScanDir(workspace.DefaultCloneDir, "Workspace"));

            if (!string.IsNullOrEmpty(Preferences.Instance.GitDefaultCloneDir))
                ScanDirs.Add(new Models.ScanDir(Preferences.Instance.GitDefaultCloneDir, "Global"));

            if (ScanDirs.Count > 0)
                _selected = ScanDirs[0];

            GetManagedRepositories(Preferences.Instance.RepositoryNodes, _managed);
        }

        public override async Task<bool> Sure()
        {
            ProgressDescription = $"Scan repositories under '{_selected.Path}' ...";

            {
                var watch = new Stopwatch();
                watch.Start();

                var rootDir = new DirectoryInfo(_selected.Path);
                var found = new List<string>();
                GetUnmanagedRepositories(rootDir, found, new EnumerationOptions()
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = true,
                });

                // Make sure this task takes at least 0.5s to avoid that the popup panel do not disappear very quickly.
                var remain = 500 - (int)watch.Elapsed.TotalMilliseconds;
                watch.Stop();
                if (remain > 0)
                    Task.Delay(remain).Wait();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var normalizedRoot = rootDir.FullName.Replace('\\', '/').TrimEnd('/');

                    foreach (var f in found)
                    {
                        var parent = new DirectoryInfo(f).Parent!.FullName.Replace('\\', '/').TrimEnd('/');
                        if (parent.Equals(normalizedRoot, StringComparison.Ordinal))
                        {
                            Preferences.Instance.FindOrAddNodeByRepositoryPath(f, null, false, false);
                        }
                        else if (parent.StartsWith(normalizedRoot, StringComparison.Ordinal))
                        {
                            var relative = parent.Substring(normalizedRoot.Length).TrimStart('/');
                            var group = FindOrCreateGroupRecursive(Preferences.Instance.RepositoryNodes, relative);
                            Preferences.Instance.FindOrAddNodeByRepositoryPath(f, group, false, false);
                        }
                    }

                    Preferences.Instance.AutoRemoveInvalidNode();
                    Preferences.Instance.Save();

                    Welcome.Instance.Refresh();
                });

                return true;
            }
        }

        private void GetManagedRepositories(List<RepositoryNode> group, HashSet<string> repos)
        {
            foreach (var node in group)
            {
                if (node.IsRepository)
                    repos.Add(node.Id);
                else
                    GetManagedRepositories(node.SubNodes, repos);
            }
        }

        private void GetUnmanagedRepositories(DirectoryInfo dir, List<string> outs, EnumerationOptions opts, int depth = 0)
        {
            var subdirs = dir.GetDirectories("*", opts);
            foreach (var subdir in subdirs)
            {
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                CallUIThread(() => ProgressDescription = $"Scanning {subdir.FullName}...");

                var normalizedSelf = subdir.FullName.Replace('\\', '/').TrimEnd('/');
                if (_managed.Contains(normalizedSelf))
                    continue;

                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var test = new Commands.QueryRepositoryRootPath(subdir.FullName).ReadToEnd();
                    if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                    {
                        var normalized = test.StdOut.Trim().Replace('\\', '/').TrimEnd('/');
                        if (!_managed.Contains(normalized))
                            outs.Add(normalized);
                    }

                    continue;
                }

                var isBare = new Commands.IsBareRepository(subdir.FullName).Result();
                if (isBare)
                {
                    outs.Add(normalizedSelf);
                    continue;
                }

                if (depth < 5)
                    GetUnmanagedRepositories(subdir, outs, opts, depth + 1);
            }
        }

        private RepositoryNode FindOrCreateGroupRecursive(List<RepositoryNode> collection, string path)
        {
            RepositoryNode node = null;
            foreach (var name in path.Split('/'))
            {
                node = FindOrCreateGroup(collection, name);
                collection = node.SubNodes;
            }

            return node;
        }

        private RepositoryNode FindOrCreateGroup(List<RepositoryNode> collection, string name)
        {
            foreach (var node in collection)
            {
                if (node.Name.Equals(name, StringComparison.Ordinal))
                    return node;
            }

            var added = new RepositoryNode()
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                IsRepository = false,
                IsExpanded = true,
            };
            collection.Add(added);

            Preferences.Instance.SortNodes(collection);
            return added;
        }

        private HashSet<string> _managed = new();
        private Models.ScanDir _selected = null;
    }
}
