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
            var builder = new StringBuilder();
            builder.Append("log -1000 --date-order --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s ");

            if (!onlyCurrentBranch)
                builder.Append("--branches --remotes ");

            if (method == Models.CommitSearchMethod.ByAuthor)
            {
                builder.Append("-i --author=").Append(filter.Quoted());
            }
            else if (method == Models.CommitSearchMethod.ByCommitter)
            {
                builder.Append("-i --committer=").Append(filter.Quoted());
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
                App.RaiseException(Context, $"Failed to query commits. Reason: {e.Message}");
            }

            return commits;
        }

        private bool _findFirstMerged = false;
        private bool _isHeadFound = false;
    }
}
