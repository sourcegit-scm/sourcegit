using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     Filesystem scanner that discovers git repositories under a root directory.
    ///     It is intentionally free of side effects: it does not touch Preferences and
    ///     does not register anything, unlike ViewModels.ScanRepositories which both
    ///     scans and imports. It is safe to call from a background thread.
    ///
    ///     Unlike the upstream implementation, it does NOT spawn one `git` process per
    ///     subdirectory to detect bare repositories. That made a scan of a few thousand
    ///     folders take minutes. A bare repository is recognised by its layout instead
    ///     (HEAD + objects/ + refs/), and only real candidates are verified by git.
    /// </summary>
    public static class RepositoryScanner
    {
        public const int MAX_DEPTH = 5;
        public const int VERIFY_CONCURRENCY = 4;

        /// <summary>
        ///     Returns absolute paths (forward-slash normalized) of the repositories found
        ///     under <paramref name="rootDir"/>, including rootDir itself when it is one.
        ///     <paramref name="progress"/> reports the directory currently being inspected;
        ///     <paramref name="repositoryFound"/> is called as soon as a repository is
        ///     confirmed, so the caller can render it incrementally.
        /// </summary>
        public static async Task<List<string>> ScanAsync(string rootDir,
            IProgress<string> progress = null,
            IProgress<string> repositoryFound = null,
            CancellationToken token = default)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
            {
                ScanLog.Write($"scan: root not found '{rootDir}'");
                return found;
            }

            ScanLog.Write($"scan: begin root='{rootDir}'");

            var root = new DirectoryInfo(rootDir);
            var opts = new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
            };

            var seen = new HashSet<string>();
            var gate = new SemaphoreSlim(VERIFY_CONCURRENCY);
            progress?.Report(root.FullName);

            if (await IsRepositoryAsync(root.FullName))
            {
                var normalized = Normalize(root.FullName);
                seen.Add(Key(normalized));
                found.Add(normalized);
                repositoryFound?.Report(normalized);
                ScanLog.Write($"scan: root itself is a repository '{normalized}'");
                return found;
            }

            await ScanChildrenAsync(root, found, seen, opts, gate, progress, repositoryFound, 0, token);
            ScanLog.Write($"scan: done, {found.Count} repositories");
            return found;
        }

        public static string Normalize(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        public static string Key(string path)
        {
            return OperatingSystem.IsLinux() ? Normalize(path) : Normalize(path).ToLower(CultureInfo.CurrentCulture);
        }

        private static bool HasGitEntry(DirectoryInfo dir)
        {
            var gitDir = Path.Combine(dir.FullName, ".git");
            return Directory.Exists(gitDir) || File.Exists(gitDir);
        }

        /// <summary>
        ///     Cheap layout check for a bare repository: `HEAD` file plus `objects/` and
        ///     `refs/` directories. This replaces a `git rev-parse` call per directory;
        ///     only directories passing this check are verified by git afterwards.
        /// </summary>
        private static bool LooksLikeBareRepository(DirectoryInfo dir)
        {
            return File.Exists(Path.Combine(dir.FullName, "HEAD")) &&
                   Directory.Exists(Path.Combine(dir.FullName, "objects")) &&
                   Directory.Exists(Path.Combine(dir.FullName, "refs"));
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
            var test = await new Commands.QueryRepositoryRootPath(dir).GetResultAsync().ConfigureAwait(false);
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                ScanLog.Write($"verify: NOT a repository '{dir}' (exitOk={test.IsSuccess}) stderr='{test.StdErr?.Trim()}'");
                return null;
            }

            return Normalize(test.StdOut.Trim());
        }

        private static async Task ScanChildrenAsync(DirectoryInfo dir, List<string> outs, HashSet<string> seen, EnumerationOptions opts,
            SemaphoreSlim gate, IProgress<string> progress, IProgress<string> repositoryFound, int depth, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            var candidates = new List<DirectoryInfo>();
            var bareCandidates = new List<DirectoryInfo>();
            var pending = new List<DirectoryInfo>();

            foreach (var subdir in dir.GetDirectories("*", opts))
            {
                if (token.IsCancellationRequested)
                    return;

                // Same pruning rules as ViewModels.ScanRepositories: hidden folders and
                // node_modules never contain repositories worth listing.
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                progress?.Report(subdir.FullName);

                if (HasGitEntry(subdir))
                {
                    ScanLog.Write($"visit: '{subdir.FullName}' has .git entry");
                    candidates.Add(subdir);
                }
                else if (LooksLikeBareRepository(subdir))
                {
                    ScanLog.Write($"visit: '{subdir.FullName}' looks like a bare repository");
                    bareCandidates.Add(subdir);
                }
                else
                {
                    pending.Add(subdir);
                }
            }

            if (candidates.Count > 0)
            {
                var tasks = new Task<string>[candidates.Count];
                for (var i = 0; i < candidates.Count; i++)
                {
                    var path = candidates[i].FullName;
                    tasks[i] = Task.Run(async () =>
                    {
                        await gate.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            return await TryGetRepositoryRootAsync(path).ConfigureAwait(false);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }, token);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                // Read results in index order so the output stays deterministic.
                for (var i = 0; i < tasks.Length; i++)
                {
                    var repo = tasks[i].Result;
                    if (repo != null && seen.Add(Key(repo)))
                    {
                        ScanLog.Write($"found: '{repo}' (root was '{candidates[i].FullName}')");
                        outs.Add(repo);
                        repositoryFound?.Report(repo);
                    }
                    else if (repo != null)
                    {
                        ScanLog.Write($"found: duplicate skipped '{repo}'");
                    }
                }
            }

            foreach (var bare in bareCandidates)
            {
                if (token.IsCancellationRequested)
                    return;

                if (!await new Commands.IsBareRepository(bare.FullName).GetResultAsync().ConfigureAwait(false))
                    continue;

                var normalized = Normalize(bare.FullName);
                if (seen.Add(Key(normalized)))
                {
                    outs.Add(normalized);
                    repositoryFound?.Report(normalized);
                }
            }

            if (depth >= MAX_DEPTH)
                return;

            foreach (var subdir in pending)
            {
                if (token.IsCancellationRequested)
                    return;

                // Do not descend into a repository: nested repositories are a separate
                // concern and would produce a confusing tree.
                await ScanChildrenAsync(subdir, outs, seen, opts, gate, progress, repositoryFound, depth + 1, token).ConfigureAwait(false);
            }
        }
    }
}
