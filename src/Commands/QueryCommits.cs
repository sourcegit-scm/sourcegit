using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        public QueryCommits(string repo, string limits, bool markMerged = true)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s {limits}";
            _markMerged = markMerged;
        }

        public QueryCommits(string repo, string filter, Models.CommitSearchMethod method, bool onlyCurrentBranch)
        {
            if (method == Models.CommitSearchMethod.Mixed)
            {
                WorkingDirectory = repo;
                Context = repo;
                _mixedFilter = filter;
                // NUL-delimited fields preserve multiline messages. Limit matching results,
                // rather than limiting the history before testing author/message/SHA.
                Args = "log --date-order --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s%x00%B%x00 " +
                    (onlyCurrentBranch ? "HEAD" : "--branches --remotes HEAD");
                return;
            }

            var builder = new StringBuilder();
            builder.Append("log -1000 --date-order --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s ");

            if (!onlyCurrentBranch)
                builder.Append("--branches --remotes ");

            if (method == Models.CommitSearchMethod.ByAuthor)
            {
                builder.Append("-i --author=").Append(filter.Quoted());
            }
            else if (method == Models.CommitSearchMethod.ByMessage)
            {
                var words = filter.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                    builder.Append("--grep=").Append(word.Trim().Quoted()).Append(' ');
                builder.Append("--all-match -i");
            }
            else if (method == Models.CommitSearchMethod.ByPath)
            {
                builder.Append("-- ").Append(filter.Quoted());
            }
            else
            {
                builder.Append("-G").Append(filter.Quoted());
            }

            WorkingDirectory = repo;
            Context = repo;
            Args = builder.ToString();
            _markMerged = false;
        }

        public async Task<List<Models.Commit>> GetResultAsync()
        {
            if (_mixedFilter != null)
                return await GetMixedResultAsync().ConfigureAwait(false);

            var commits = new List<Models.Commit>();
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                var findHead = false;
                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var parts = line.Split('\0');
                    if (parts.Length != 8)
                        continue;

                    var commit = ParseCommit(parts);
                    commits.Add(commit);

                    if (!findHead && commit.IsMerged)
                        findHead = true;
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);

                if (_markMerged && !findHead && commits.Count > 0)
                {
                    var set = await new QueryCurrentBranchCommitHashes(WorkingDirectory, commits[^1].CommitterTime)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    foreach (var c in commits)
                    {
                        if (set.Contains(c.SHA))
                        {
                            c.IsMerged = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                RaiseException($"Failed to query commits. Reason: {e.Message}");
            }

            return commits;
        }

        private async Task<List<Models.Commit>> GetMixedResultAsync()
        {
            var commits = new List<Models.Commit>();
            if (string.IsNullOrEmpty(_mixedFilter) || CancellationToken.IsCancellationRequested)
                return commits;

            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();
                var errors = proc.StandardError.ReadToEndAsync();
                try
                {
                    var fields = new string[9];
                    var fieldIndex = 0;
                    var field = new StringBuilder();
                    var buffer = new char[4096];
                    var completed = false;
                    int count;
                    while (!completed && (count = await proc.StandardOutput.ReadAsync(buffer.AsMemory(), CancellationToken).ConfigureAwait(false)) > 0)
                    {
                        for (var i = 0; i < count; i++)
                        {
                            if (buffer[i] != '\0')
                            {
                                field.Append(buffer[i]);
                                continue;
                            }

                            fields[fieldIndex++] = field.ToString();
                            field.Clear();
                            if (fieldIndex < fields.Length)
                                continue;

                            // git inserts a newline between formatted records.
                            fields[0] = fields[0].TrimStart('\r', '\n');
                            if (fields[0].StartsWith(_mixedFilter, StringComparison.OrdinalIgnoreCase) ||
                                fields[3].Contains(_mixedFilter, StringComparison.OrdinalIgnoreCase) ||
                                fields[8].Contains(_mixedFilter, StringComparison.OrdinalIgnoreCase))
                            {
                                commits.Add(ParseCommit(fields));
                                if (commits.Count == 1000)
                                {
                                    completed = true;
                                    break;
                                }
                            }
                            fieldIndex = 0;
                        }
                    }
                }
                finally
                {
                    // Stop promptly on cancellation or when enough matches have been found.
                    try
                    {
                        if (!proc.HasExited)
                            proc.Kill(true);
                    }
                    catch (InvalidOperationException)
                    {
                        // The process can exit between HasExited and Kill.
                    }
                    await proc.WaitForExitAsync().ConfigureAwait(false);
                    await errors.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // A newer search or closing the search panel cancels this read-only query.
            }
            catch (Exception e)
            {
                RaiseException($"Failed to query commits. Reason: {e.Message}");
            }
            return commits;
        }

        private static Models.Commit ParseCommit(string[] parts)
        {
            var commit = new Models.Commit() { SHA = parts[0] };
            commit.ParseParents(parts[1]);
            commit.ParseDecorators(parts[2]);
            commit.Author = Models.User.FindOrAdd(parts[3]);
            commit.AuthorTime = ulong.Parse(parts[4]);
            commit.Committer = Models.User.FindOrAdd(parts[5]);
            commit.CommitterTime = ulong.Parse(parts[6]);
            commit.Subject = parts[7];
            return commit;
        }

        private string _mixedFilter = null;
        private bool _markMerged = false;
    }
}
