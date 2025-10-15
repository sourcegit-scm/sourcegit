using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCommitsForInteractiveRebase : Command
    {
        public QueryCommitsForInteractiveRebase(string repo, string on)
        {
            _boundary = $"----- BOUNDARY OF COMMIT {Guid.NewGuid()} -----";

            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --topo-order --cherry-pick --right-only --no-merges --no-show-signature --decorate=full --format=\"%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%B%n{_boundary}\" {on}...HEAD";
        }

        public async Task<List<Models.InteractiveCommit>> GetResultAsync()
        {
            var commits = new List<Models.InteractiveCommit>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
            {
                App.RaiseException(Context, $"Failed to query commits for interactive-rebase. Reason: {rs.StdErr}");
                return commits;
            }

            Models.InteractiveCommit current = null;

            var nextPartIdx = 0;
            var start = 0;
            var end = rs.StdOut.IndexOf('\n', start);
            while (end > 0)
            {
                var line = rs.StdOut.Substring(start, end - start);
                switch (nextPartIdx)
                {
                    case 0:
                        current = new Models.InteractiveCommit();
                        current.Commit.SHA = line;
                        commits.Add(current);
                        break;
                    case 1:
                        current.Commit.ParseParents(line);
                        break;
                    case 2:
                        current.Commit.ParseDecorators(line);
                        break;
                    case 3:
                        current.Commit.Author = Models.User.FindOrAdd(line);
                        break;
                    case 4:
                        current.Commit.AuthorTime = ulong.Parse(line);
                        break;
                    case 5:
                        current.Commit.Committer = Models.User.FindOrAdd(line);
                        break;
                    case 6:
                        current.Commit.CommitterTime = ulong.Parse(line);
                        break;
                    default:
                        var boundary = rs.StdOut.IndexOf(_boundary, end + 1, StringComparison.Ordinal);
                        if (boundary > end)
                        {
                            current.Message = rs.StdOut.Substring(start, boundary - start - 1);
                            end = boundary + _boundary.Length;
                        }
                        else
                        {
                            current.Message = rs.StdOut.Substring(start);
                            end = rs.StdOut.Length - 2;
                        }

                        nextPartIdx = -1;
                        break;
                }

                nextPartIdx++;

                start = end + 1;
                if (start >= rs.StdOut.Length - 1)
                    break;

                end = rs.StdOut.IndexOf('\n', start);
            }

            return commits;
        }

        private readonly string _boundary;
    }
}
