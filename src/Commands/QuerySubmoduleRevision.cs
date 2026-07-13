using System;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QuerySubmoduleRevision : Command
    {
        public QuerySubmoduleRevision(string repo, string revision)
        {
            WorkingDirectory = repo;
            Context = repo;

            if (revision.EndsWith("-dirty", StringComparison.Ordinal))
            {
                _hasUncommittedChange = true;
                _revision = revision.Substring(0, revision.Length - 6);
            }
            else
            {
                _hasUncommittedChange = false;
                _revision = revision;
            }
        }

        public async Task<Models.RevisionSubmodule> GetResultAsync()
        {
            Args = $"show --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s%x00%B -s {_revision}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess || string.IsNullOrEmpty(rs.StdOut))
                return null;

            var commit = new Models.Commit();
            var lines = rs.StdOut.Split('\0');
            if (lines.Length < 9)
                return null;

            commit.SHA = lines[0];
            if (!string.IsNullOrEmpty(lines[1]))
                commit.Parents.AddRange(lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (!string.IsNullOrEmpty(lines[2]))
                commit.ParseDecorators(lines[2]);
            commit.Author = Models.User.FindOrAdd(lines[3]);
            commit.AuthorTime = ulong.Parse(lines[4]);
            commit.Committer = Models.User.FindOrAdd(lines[5]);
            commit.CommitterTime = ulong.Parse(lines[6]);
            commit.Subject = lines[7];

            var message = new Models.CommitFullMessage() { Message = lines[8].TrimEnd() };
            var uncommittedChangesCount = 0;
            if (_hasUncommittedChange)
                uncommittedChangesCount = await new CountLocalChanges(WorkingDirectory, true)
                    .GetResultAsync()
                    .ConfigureAwait(false);

            return new Models.RevisionSubmodule()
            {
                Commit = commit,
                FullMessage = message,
                UncommittedChanges = uncommittedChangesCount
            };
        }

        private bool _hasUncommittedChange = false;
        private string _revision = string.Empty;
    }
}
