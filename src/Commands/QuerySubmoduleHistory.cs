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

                var section = new Models.SubmoduleHistory { OwnerSHA = commit.SHA, Path = path, NewSHA = next };
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

        public async Task LoadAsync(Models.SubmoduleHistory section, Models.SubmoduleHistoryMode mode, int limit)
        {
            section.Commits = [];
            section.Error = string.Empty;
            section.IsTruncated = false;
            try
            {
                var root = System.IO.Path.GetFullPath(repository);
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, section.Path));
                if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    !(File.Exists(System.IO.Path.Combine(path, ".git")) || Directory.Exists(System.IO.Path.Combine(path, ".git"))))
                    throw new InvalidOperationException("SubmoduleHistory.Unavailable");

                section.RepositoryPath = path;
                var tips = section.Previous.Keys.ToList();
                if (section.NewSHA != null)
                    tips.Insert(0, section.NewSHA);
                foreach (var tip in tips.Distinct())
                    await GitAsync(path, ["cat-file", "-e", tip + "^{commit}"]).ConfigureAwait(false);

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
                section.Commits = commits;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                section.Error = e is InvalidOperationException && e.Message == "SubmoduleHistory.Unavailable"
                    ? "SubmoduleHistory.Unavailable" : "SubmoduleHistory.MissingObjects";
            }
        }

        public Task<string> ReadCommitAsync(string path, string sha) =>
            GitAsync(path, ["show", "--no-ext-diff", "--no-textconv", "--no-show-signature", "--format=fuller", "--stat", sha, "--"]);

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
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("--no-pager");
            foreach (var arg in args)
                process.StartInfo.ArgumentList.Add(arg);
            process.Start();
            using var registration = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); }
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
