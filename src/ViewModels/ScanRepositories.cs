using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public class ScanRepositories : Popup
    {
        public string RootDir
        {
            get;
        }

        public ScanRepositories(string rootDir)
        {
            GetManagedRepositories(Preferences.Instance.RepositoryNodes, _managed);
            RootDir = rootDir;
        }

        public override Task<bool> Sure()
        {
            ProgressDescription = $"Scan repositories under '{RootDir}' ...";

            return Task.Run(() =>
            {
                var watch = new Stopwatch();
                watch.Start();

                var rootDir = new DirectoryInfo(RootDir);
                var founded = new List<FoundRepository>();
                GetUnmanagedRepositories(rootDir, founded, new EnumerationOptions()
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = true,
                });

                Dispatcher.UIThread.Invoke(() =>
                {
                    var normalizedRoot = rootDir.FullName.Replace("\\", "/");

                    foreach (var f in founded)
                    {
                        var parent = new DirectoryInfo(f.Path).Parent!.FullName.Replace("\\", "/");
                        if (parent.Equals(normalizedRoot, StringComparison.Ordinal))
                        {
                            Preferences.Instance.FindOrAddNodeByRepositoryPath(f.Path, null, false);
                        }
                        else if (parent.StartsWith(normalizedRoot, StringComparison.Ordinal))
                        {
                            var relative = parent.Substring(normalizedRoot.Length).TrimStart('/');
                            var group = FindOrCreateGroupRecursive(Preferences.Instance.RepositoryNodes, relative);
                            Preferences.Instance.FindOrAddNodeByRepositoryPath(f.Path, group, false);
                        }
                    }

                    Preferences.Instance.AutoRemoveInvalidNode();
                    Welcome.Instance.Refresh();
                });

                // Make sure this task takes at least 0.5s to avoid that the popup panel do not disappear very quickly.
                var remain = 500 - (int)watch.Elapsed.TotalMilliseconds;
                watch.Stop();
                if (remain > 0)
                    Task.Delay(remain).Wait();

                return true;
            });
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

        private void GetUnmanagedRepositories(DirectoryInfo dir, List<FoundRepository> outs, EnumerationOptions opts, int depth = 0)
        {
            var subdirs = dir.GetDirectories("*", opts);
            foreach (var subdir in subdirs)
            {
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                CallUIThread(() => ProgressDescription = $"Scanning {subdir.FullName}...");

                var normalizedSelf = subdir.FullName.Replace("\\", "/");
                if (_managed.Contains(normalizedSelf))
                    continue;

                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var test = new Commands.QueryRepositoryRootPath(subdir.FullName).ReadToEnd();
                    if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                    {
                        var normalized = test.StdOut.Trim().Replace("\\", "/");
                        if (!_managed.Contains(normalized))
                            outs.Add(new FoundRepository(normalized, false));
                    }

                    continue;
                }

                var isBare = new Commands.IsBareRepository(subdir.FullName).Result();
                if (isBare)
                {
                    outs.Add(new FoundRepository(normalizedSelf, true));
                    continue;
                }

                if (depth < 5)
                    GetUnmanagedRepositories(subdir, outs, opts, depth + 1);
            }
        }

        private RepositoryNode FindOrCreateGroupRecursive(List<RepositoryNode> collection, string path)
        {
            var idx = path.IndexOf('/');
            if (idx < 0)
                return FindOrCreateGroup(collection, path);

            var name = path.Substring(0, idx);
            var tail = path.Substring(idx + 1);
            var parent = FindOrCreateGroup(collection, name);
            return FindOrCreateGroupRecursive(parent.SubNodes, tail);
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
            collection.Sort((l, r) =>
            {
                if (l.IsRepository != r.IsRepository)
                    return l.IsRepository ? 1 : -1;

                return string.Compare(l.Name, r.Name, StringComparison.Ordinal);
            });

            return added;
        }

        private record FoundRepository(string path, bool isBare)
        {
            public string Path { get; set; } = path;
            public bool IsBare { get; set; } = isBare;
        }

        private HashSet<string> _managed = new HashSet<string>();
    }
}
