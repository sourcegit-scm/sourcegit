using System.Collections.Generic;
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
        } = string.Empty;

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
                // If it is too fast, the panel will dispear very quickly, the we'll have a bad experience.
                Task.Delay(500).Wait();

                var founded = new List<string>();
                GetUnmanagedRepositories(new DirectoryInfo(RootDir), founded, new EnumerationOptions()
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = true,
                });

                Dispatcher.UIThread.Invoke(() =>
                {
                    foreach (var f in founded)
                        Preference.Instance.FindOrAddNodeByRepositoryPath(f, null, false);
                    Welcome.Instance.Refresh();
                });

                return true;
            });
        }

        private void GetManagedRepositories(List<RepositoryNode> group, HashSet<string> repos)
        {
            foreach (RepositoryNode node in group)
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
                        if (!_managed.Contains(normalizedSelf))
                            outs.Add(normalized);

                        continue;
                    }
                }

                if (depth < 8)
                    GetUnmanagedRepositories(subdir, outs, opts, depth + 1);
            }
        }

        private HashSet<string> _managed = new HashSet<string>();
    }
}
