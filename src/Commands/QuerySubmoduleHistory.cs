using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    // All commands are read-only and use argument lists (paths may contain spaces or quotes).
    public class QuerySubmoduleHistory(string repository, CancellationToken cancellationToken = default)
    {
        public async Task<List<Models.SubmoduleHistory>> GetChangesAsync(Models.Commit commit)
        {
            var current = await ReadLinksAsync(commit.SHA).ConfigureAwait(false);
            var parents = new List<Dictionary<string, string>>();
            foreach (var parent in commit.Parents)
                parents.Add(await ReadLinksAsync(parent).ConfigureAwait(false));

            var paths = current.Keys.Concat(parents.SelectMany(x => x.Keys)).Distinct().Order(StringComparer.Ordinal);
            var result = new List<Models.SubmoduleHistory>();
            foreach (var path in paths)
            {
                current.TryGetValue(path, out var next);
                if (parents.Count > 0 && parents.All(p => p.GetValueOrDefault(path) == next))
                    continue;

                var section = new Models.SubmoduleHistory
                {
                    OwnerSHA = commit.SHA,
                    ParentHistory = commit.SubmoduleHistory,
                    LocalPath = path,
                    Path = commit.SubmoduleHistory == null ? path : commit.SubmoduleHistory.Path + "/" + path,
                    NewSHA = next,
                };
                for (var i = 0; i < parents.Count; i++)
                {
                    if (!parents[i].TryGetValue(path, out var old) || section.Previous.ContainsKey(old))
                        continue;
                    var previousChange = await GitAsync(repository, ["log", "-1", "--format=%H", commit.Parents[i], "--", path]).ConfigureAwait(false);
                    section.Previous[old] = previousChange.Trim();
                }
                result.Add(section);
            }
            return result;
        }

        public async Task ReadPreviewAsync(Models.SubmoduleHistory section)
        {
            section.Error = string.Empty;
            try
            {
                section.RepositoryPath = ResolvePath(section);
                var tips = section.Previous.Keys.ToList();
                if (section.NewSHA != null)
                    tips.Insert(0, section.NewSHA);
                await ReadIdentityAsync(section, tips[0]).ConfigureAwait(false);
                var args = new List<string> { "log", "--topo-order", "--max-count=1", "--no-show-signature", "--format=%H%x00%P%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s" };
                args.AddRange(tips.Distinct());
                args.Add("--");
                section.PreviewCommit = ParseLog(await GitAsync(section.RepositoryPath, args).ConfigureAwait(false)).FirstOrDefault();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                section.Error = e is InvalidOperationException && e.Message == "SubmoduleHistory.Unavailable"
                    ? "SubmoduleHistory.Unavailable" : "SubmoduleHistory.MissingObjects";
            }
        }

        public async Task LoadAsync(Models.SubmoduleHistory section, Models.SubmoduleHistoryMode mode, int limit)
        {
            section.Commits = [];
            section.Error = string.Empty;
            section.IsTruncated = false;
            try
            {
                var path = ResolvePath(section);
                section.RepositoryPath = path;
                var tips = section.Previous.Keys.ToList();
                if (section.NewSHA != null)
                    tips.Insert(0, section.NewSHA);
                foreach (var tip in tips.Distinct())
                    await GitAsync(path, ["cat-file", "-e", tip + "^{commit}"]).ConfigureAwait(false);

                await ReadIdentityAsync(section, tips[0]).ConfigureAwait(false);
                limit = Math.Max(1, limit);
                var args = new List<string> { "log", "--topo-order", "--no-show-signature", "--format=%H%x00%P%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s", $"--max-count={limit + 1}" };
                if (mode == Models.SubmoduleHistoryMode.FullHistory)
                {
                    args.AddRange(tips.Distinct());
                }
                else if (section.NewSHA != null)
                {
                    args.Add(section.NewSHA);
                    if (section.Previous.Count > 0)
                    {
                        args.Add("--not");
                        args.AddRange(section.Previous.Keys);
                    }
                }
                else
                {
                    args.Add("--no-walk");
                    args.AddRange(tips.Distinct());
                }
                args.Add("--");
                var commits = ParseLog(await GitAsync(path, args).ConfigureAwait(false));
                section.IsTruncated = commits.Count > limit;
                if (section.IsTruncated)
                    commits.RemoveAt(commits.Count - 1);

                if (mode == Models.SubmoduleHistoryMode.ChangedRange)
                {
                    // Include the old pointer as an explicit boundary, also for rewinds.
                    foreach (var old in section.Previous.Keys)
                    {
                        if (commits.Any(c => c.SHA == old))
                            continue;
                        var boundary = await GitAsync(path, ["log", "-1", "--no-show-signature", "--format=%H%x00%P%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s", old, "--"]).ConfigureAwait(false);
                        commits.AddRange(ParseLog(boundary));
                    }
                    if (section.NewSHA != null && commits.All(c => c.SHA != section.NewSHA))
                    {
                        var tip = await GitAsync(path, ["log", "-1", "--no-show-signature", "--format=%H%x00%P%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s", section.NewSHA, "--"]).ConfigureAwait(false);
                        commits.AddRange(ParseLog(tip));
                    }
                }
                // Boundary tips can be ancestors of one another (rewinds and merge commits).
                // Keep children before their parents even when boundaries were appended later.
                var positions = commits.Select((c, i) => (c.SHA, i)).ToDictionary(x => x.SHA, x => x.i);
                var childCounts = commits.ToDictionary(c => c.SHA, _ => 0);
                foreach (var commit in commits)
                {
                    foreach (var parent in commit.Parents)
                    {
                        if (childCounts.TryGetValue(parent, out var count))
                            childCounts[parent] = count + 1;
                    }
                }
                var ready = new PriorityQueue<Models.Commit, int>();
                foreach (var commit in commits)
                {
                    if (childCounts[commit.SHA] == 0)
                        ready.Enqueue(commit, positions[commit.SHA]);
                }
                var ordered = new List<Models.Commit>();
                while (ready.TryDequeue(out var commit, out _))
                {
                    ordered.Add(commit);
                    foreach (var parent in commit.Parents)
                    {
                        if (childCounts.TryGetValue(parent, out var count))
                        {
                            childCounts[parent] = count - 1;
                            if (count == 1)
                                ready.Enqueue(commits[positions[parent]], positions[parent]);
                        }
                    }
                }
                section.Commits = ordered;
                section.PreviewCommit = ordered.FirstOrDefault();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                section.Error = e is InvalidOperationException && e.Message == "SubmoduleHistory.Unavailable"
                    ? "SubmoduleHistory.Unavailable" : "SubmoduleHistory.MissingObjects";
            }
        }

        private string ResolvePath(Models.SubmoduleHistory section)
        {
            var root = System.IO.Path.GetFullPath(repository);
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, section.LocalPath ?? section.Path));
            if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !(File.Exists(System.IO.Path.Combine(path, ".git")) || Directory.Exists(System.IO.Path.Combine(path, ".git"))))
                throw new InvalidOperationException("SubmoduleHistory.Unavailable");
            return path;
        }

        public async Task<Models.SubmodulePointerStatus> ReadPointerStatusAsync(Models.SubmoduleHistory section)
        {
            try
            {
                var path = ResolvePath(section);
                var head = (await GitAsync(path, ["rev-parse", "--verify", "HEAD^{commit}"]).ConfigureAwait(false)).Trim();
                return new Models.SubmodulePointerStatus
                {
                    LocalHEAD = head,
                    State = section.NewSHA == null ? Models.SubmodulePointerState.Removed :
                        head == section.NewSHA ? Models.SubmodulePointerState.Matches : Models.SubmodulePointerState.Differs,
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return new Models.SubmodulePointerStatus { State = Models.SubmodulePointerState.Unavailable };
            }
        }

        public async Task<Models.SubmoduleComparison> CompareAsync(Models.SubmoduleHistory section, string previousSHA, int limit)
        {
            if (previousSHA == null ? section.Previous.Count > 0 : !section.Previous.ContainsKey(previousSHA))
                throw new ArgumentException("Unknown previous gitlink", nameof(previousSHA));
            var path = ResolvePath(section);
            var empty = (await GitAsync(path, ["hash-object", "-t", "tree", "--stdin"]).ConfigureAwait(false)).Trim();
            var from = previousSHA ?? empty;
            var to = section.NewSHA ?? empty;
            var added = await ReadRange(section.NewSHA, previousSHA).ConfigureAwait(false);
            var removed = await ReadRange(previousSHA, section.NewSHA).ConfigureAwait(false);
            var patch = await GitAsync(path, ["diff", "--no-ext-diff", "--no-textconv", "--no-color", "--ignore-submodules=none", "--submodule=short", "--stat", "--patch", from, to, "--"]).ConfigureAwait(false);
            return new Models.SubmoduleComparison { Added = added, Removed = removed, Patch = patch };

            async Task<string> ReadRange(string tip, string excluded)
            {
                if (tip == null)
                    return string.Empty;
                var args = new List<string> { "log", "--no-show-signature", "--format=%h %s", "--topo-order", $"--max-count={Math.Max(1, limit)}", tip };
                if (excluded != null)
                    args.AddRange(["--not", excluded]);
                args.Add("--");
                return await GitAsync(path, args).ConfigureAwait(false);
            }
        }

        public Task<string> ReadCommitAsync(string path, string sha) =>
            GitAsync(path, ["show", "--no-ext-diff", "--no-textconv", "--no-show-signature", "--format=fuller", "--stat", sha, "--"]);

        private async Task ReadIdentityAsync(Models.SubmoduleHistory section, string tip)
        {
            var origin = await TryGitAsync(section.RepositoryPath, ["config", "--get", "remote.origin.url"]).ConfigureAwait(false);
            section.RepositoryName = Models.SubmoduleHistory.GetRepositoryName(origin.Trim(), System.IO.Path.GetFileName(section.Path));
            var current = (await TryGitAsync(section.RepositoryPath, ["symbolic-ref", "--quiet", "HEAD"]).ConfigureAwait(false)).Trim();
            var refs = (await GitAsync(section.RepositoryPath,
                ["for-each-ref", "--format=%(refname)", "--points-at", tip, "refs/heads/", "refs/remotes/"]).ConfigureAwait(false))
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(r => !r.EndsWith("/HEAD", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
            var branch = refs.Contains(current) ? current : refs.FirstOrDefault();
            section.BranchName = branch == null ? $"detached@{tip[..Math.Min(8, tip.Length)]}" :
                branch.StartsWith("refs/heads/", StringComparison.Ordinal) ? branch[11..] : branch[13..];
        }

        private async Task<string> TryGitAsync(string path, IEnumerable<string> args)
        {
            try
            {
                return await GitAsync(path, args).ConfigureAwait(false);
            }
            catch (InvalidOperationException) { return string.Empty; }
        }

        private async Task<Dictionary<string, string>> ReadLinksAsync(string revision)
        {
            var output = await GitAsync(repository, ["ls-tree", "-r", "-z", revision]).ConfigureAwait(false);
            var links = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var tab = entry.IndexOf('\t');
                if (tab > 0 && entry.StartsWith("160000 commit ", StringComparison.Ordinal))
                    links[entry[(tab + 1)..]] = entry[14..tab];
            }
            return links;
        }

        private static List<Models.Commit> ParseLog(string text)
        {
            var result = new List<Models.Commit>();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = line.Split('\0');
                if (fields.Length != 7)
                    continue;
                var commit = new Models.Commit
                {
                    SHA = fields[0],
                    Author = Models.User.FindOrAdd(fields[2]),
                    AuthorTime = ulong.Parse(fields[3]),
                    Committer = Models.User.FindOrAdd(fields[4]),
                    CommitterTime = ulong.Parse(fields[5]),
                    Subject = fields[6].TrimEnd('\r'),
                };
                commit.ParseParents(fields[1]);
                result.Add(commit);
            }
            return result;
        }

        private async Task<string> GitAsync(string path, IEnumerable<string> args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrEmpty(Native.OS.GitExecutable) ? "git" : Native.OS.GitExecutable,
                WorkingDirectory = path,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // A history viewer should report unavailable objects, not trigger a promisor fetch.
            process.StartInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
            process.StartInfo.ArgumentList.Add("--no-pager");
            foreach (var arg in args)
                process.StartInfo.ArgumentList.Add(arg);
            process.Start();
            process.StandardInput.Close();
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch (InvalidOperationException) { }
            });
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException(await error.ConfigureAwait(false));
            return await output.ConfigureAwait(false);
        }
    }
}
