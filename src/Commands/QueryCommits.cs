using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        public QueryCommits(string repo, string limits, bool needFindHead = true)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s {limits}";
            _findFirstMerged = needFindHead;
        }

        public QueryCommits(string repo, string filter, Models.CommitSearchMethod method, bool onlyCurrentBranch)
        {
            string search = onlyCurrentBranch ? string.Empty : "--branches --remotes ";

            if (method == Models.CommitSearchMethod.ByAuthor)
            {
                search += $"-i --author={filter.Quoted()}";
            }
            else if (method == Models.CommitSearchMethod.ByCommitter)
            {
                search += $"-i --committer={filter.Quoted()}";
            }
            else if (method == Models.CommitSearchMethod.ByMessage)
            {
                var argsBuilder = new StringBuilder();
                argsBuilder.Append(search);

                var words = filter.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                    argsBuilder.Append("--grep=").Append(word.Trim().Quoted()).Append(' ');
                argsBuilder.Append("--all-match -i");

                search = argsBuilder.ToString();
            }
            else if (method == Models.CommitSearchMethod.ByPath)
            {
                search += $"-- {filter.Quoted()}";
            }
            else
            {
                search = $"-G{filter.Quoted()}";
            }

            WorkingDirectory = repo;
            Context = repo;
            Args = $"log -1000 --date-order --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s {search}";
            _findFirstMerged = false;
        }

        public async Task<List<Models.Commit>> GetResultAsync()
        {
            var commits = new List<Models.Commit>();
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                while (await proc.StandardOutput.ReadLineAsync() is { } line)
                {
                    var parts = line.Split('\0');
                    if (parts.Length != 8)
                        continue;

                    var commit = new Models.Commit() { SHA = parts[0] };
                    commit.ParseParents(parts[1]);
                    commit.ParseDecorators(parts[2]);
                    commit.Author = Models.User.FindOrAdd(parts[3]);
                    commit.AuthorTime = ulong.Parse(parts[4]);
                    commit.Committer = Models.User.FindOrAdd(parts[5]);
                    commit.CommitterTime = ulong.Parse(parts[6]);
                    commit.Subject = parts[7];
                    commits.Add(commit);

                    if (commit.IsMerged && !_isHeadFound)
                        _isHeadFound = true;
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);

                if (_findFirstMerged && !_isHeadFound && commits.Count > 0)
                    await MarkFirstMergedAsync(commits).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                App.RaiseException(Context, $"Failed to query commits. Reason: {e.Message}");
            }

            return commits;
        }

        private async Task MarkFirstMergedAsync(List<Models.Commit> commits)
        {
            Args = $"log --since={commits[^1].CommitterTimeStr.Quoted()} --format=\"%H\"";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var shas = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (shas.Length == 0)
                return;

            var set = new HashSet<string>(shas);

            foreach (var c in commits)
            {
                if (set.Contains(c.SHA))
                {
                    c.IsMerged = true;
                    break;
                }
            }
        }

        private bool _findFirstMerged = false;
        private bool _isHeadFound = false;
    }
}
