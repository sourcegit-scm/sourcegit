using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     Filesystem scanner that discovers git repositories under a root directory.
    ///     It is intentionally free of side effects: it does not touch Preferences and
    ///     does not register anything, unlike ViewModels.ScanRepositories which both
    ///     scans and imports.
    /// </summary>
    public static class RepositoryScanner
    {
        public const int MAX_DEPTH = 5;

        /// <summary>
        ///     Returns absolute paths (forward-slash normalized) of the repositories found
        ///     under <paramref name="rootDir"/>, including rootDir itself when it is one.
        /// </summary>
        public static async Task<List<string>> ScanAsync(string rootDir, CancellationToken token = default)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
                return found;

            var root = new DirectoryInfo(rootDir);
            var opts = new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
            };

            var seen = new HashSet<string>();
            if (await IsRepositoryAsync(root.FullName))
            {
                var normalized = Normalize(root.FullName);
                seen.Add(Key(normalized));
                found.Add(normalized);
                return found;
            }

            await ScanChildrenAsync(root, found, seen, opts, 0, token);
            return found;
        }

        public static string Normalize(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private static string Key(string normalizedPath)
        {
            return OperatingSystem.IsLinux() ? normalizedPath : normalizedPath.ToLower(CultureInfo.CurrentCulture);
        }

        private static async Task<bool> IsRepositoryAsync(string dir)
        {
            var gitDir = Path.Combine(dir, ".git");
            if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
                return false;
            return await TryGetRepositoryRootAsync(dir) != null;
        }

        /// <summary>
        ///     Runs `git rev-parse --show-toplevel` to make sure the directory is really a
        ///     repository root (a plain .git folder is not enough, e.g. worktrees and
        ///     submodules report a different top level).
        /// </summary>
        private static async Task<string> TryGetRepositoryRootAsync(string dir)
        {
            var test = await new Commands.QueryRepositoryRootPath(dir).GetResultAsync();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
                return null;

            return Normalize(test.StdOut.Trim());
        }

        private static async Task ScanChildrenAsync(DirectoryInfo dir, List<string> outs, HashSet<string> seen, EnumerationOptions opts, int depth, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            foreach (var subdir in dir.GetDirectories("*", opts))
            {
                if (token.IsCancellationRequested)
                    return;

                // Same pruning rules as ViewModels.ScanRepositories: hidden folders and
                // node_modules never contain repositories worth listing.
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                var normalized = Normalize(subdir.FullName);

                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var repo = await TryGetRepositoryRootAsync(subdir.FullName);
                    if (repo != null && seen.Add(Key(repo)))
                        outs.Add(repo);

                    // Do not descend into a repository: nested repositories are a
                    // separate concern and would produce a confusing tree.
                    continue;
                }

                if (await new Commands.IsBareRepository(subdir.FullName).GetResultAsync())
                {
                    if (seen.Add(Key(normalized)))
                        outs.Add(normalized);
                    continue;
                }

                if (depth < MAX_DEPTH)
                    await ScanChildrenAsync(subdir, outs, seen, opts, depth + 1, token);
            }
        }
    }
}
