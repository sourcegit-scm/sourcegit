using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ScanRepositories : Popup
    {
        public bool UseCustomDir
        {
            get => _useCustomDir;
            set => SetProperty(ref _useCustomDir, value);
        }

        public string CustomDir
        {
            get => _customDir;
            set => SetProperty(ref _customDir, value);
        }

        public List<Models.ScanDir> ScanDirs
        {
            get;
        }

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
            else
                _useCustomDir = true;

            GetManagedRepositories(Preferences.Instance.RepositoryNodes, _managed);
        }

        public override async Task<bool> Sure()
        {
            string selectedDir;
            if (_useCustomDir)
            {
                if (string.IsNullOrEmpty(_customDir))
                {
                    App.RaiseException(null, "Missing root directory to scan!");
                    return false;
                }

                selectedDir = _customDir;
            }
            else
            {
                if (_selected == null || string.IsNullOrEmpty(_selected.Path))
                {
                    App.RaiseException(null, "Missing root directory to scan!");
                    return false;
                }

                selectedDir = _selected.Path;
            }

            if (!Directory.Exists(selectedDir))
                return true;

            ProgressDescription = $"Scan repositories under '{selectedDir}' ...";

            var minDelay = Task.Delay(500);
            var rootDir = new DirectoryInfo(selectedDir);
            var found = new List<string>();

            await GetUnmanagedRepositoriesAsync(rootDir, found, new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
            });

            // Make sure this task takes at least 0.5s to avoid the popup panel disappearing too quickly.
            await minDelay;

            var normalizedRoot = rootDir.FullName.Replace('\\', '/').TrimEnd('/');
            foreach (var f in found)
            {
                var parent = new DirectoryInfo(f).Parent!.FullName.Replace('\\', '/').TrimEnd('/');
                if (parent.Equals(normalizedRoot, StringComparison.Ordinal))
                {
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(f, null, false, false);
                    await node.UpdateStatusAsync(false, null);
                }
                else if (parent.StartsWith(normalizedRoot, StringComparison.Ordinal))
                {
                    var relative = parent.Substring(normalizedRoot.Length).TrimStart('/');
                    var group = FindOrCreateGroupRecursive(Preferences.Instance.RepositoryNodes, relative);
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(f, group, false, false);
                    await node.UpdateStatusAsync(false, null);
                }
            }

            Preferences.Instance.AutoRemoveInvalidNode();
            Preferences.Instance.Save();
            Welcome.Instance.Refresh();
            return true;
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

        private async Task GetUnmanagedRepositoriesAsync(DirectoryInfo dir, List<string> outs, EnumerationOptions opts, int depth = 0)
        {
            var subdirs = dir.GetDirectories("*", opts);
            foreach (var subdir in subdirs)
            {
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                ProgressDescription = $"Scanning {subdir.FullName}...";

                var normalizedSelf = subdir.FullName.Replace('\\', '/').TrimEnd('/');
                if (_managed.Contains(normalizedSelf))
                    continue;

                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var test = await new Commands.QueryRepositoryRootPath(subdir.FullName).GetResultAsync();
                    if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                    {
                        var normalized = test.StdOut.Trim().Replace('\\', '/').TrimEnd('/');
                        if (!_managed.Contains(normalized))
                            outs.Add(normalized);
                    }

                    continue;
                }

                var isBare = await new Commands.IsBareRepository(subdir.FullName).GetResultAsync();
                if (isBare)
                {
                    outs.Add(normalizedSelf);
                    continue;
                }

                if (depth < 5)
                    await GetUnmanagedRepositoriesAsync(subdir, outs, opts, depth + 1);
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
        private bool _useCustomDir = false;
        private string _customDir = string.Empty;
        private Models.ScanDir _selected = null;
    }
}
