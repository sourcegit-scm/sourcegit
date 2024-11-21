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
            private set;
        }

        public ScanRepositories(string rootDir)
        {
            GetManagedRepositories(Preference.Instance.RepositoryNodes, _managed);

            RootDir = rootDir;
            View = new Views.ScanRepositories() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            ProgressDescription = $"Scan repositories under '{RootDir}' ...";

            return Task.Run(() =>
            {
                var watch = new Stopwatch();
                watch.Start();

                var rootDir = new DirectoryInfo(RootDir);
                var founded = new List<string>();
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
                        var parent = new DirectoryInfo(f).Parent!.FullName.Replace("\\", "/");
                        if (parent.Equals(normalizedRoot, StringComparison.Ordinal))
                        {
                            Preference.Instance.FindOrAddNodeByRepositoryPath(f, null, false);
                        }
                        else if (parent.StartsWith(normalizedRoot, StringComparison.Ordinal))
                        {
                            var relative = parent.Substring(normalizedRoot.Length).TrimStart('/');
                            var group = FindOrCreateGroupRecursive(Preference.Instance.RepositoryNodes, relative);
                            Preference.Instance.FindOrAddNodeByRepositoryPath(f, group, false);
                        }
                    }

                    Preference.Instance.AutoRemoveInvalidNode();
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

        private void GetUnmanagedRepositories(DirectoryInfo dir, List<string> outs, EnumerationOptions opts, int depth = 0)
        {
            var subdirs = dir.GetDirectories("*", opts);
            foreach (var subdir in subdirs)
            {
                SetProgressDescription($"Scanning {subdir.FullName}...");

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
                            outs.Add(normalized);
                    }

                    continue;
                }

                if (depth < 8)
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

        private HashSet<string> _managed = new HashSet<string>();
    }
}
