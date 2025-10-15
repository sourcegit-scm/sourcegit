using System;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QuerySingleCommit : Command
    {
        public QuerySingleCommit(string repo, string sha)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"show --no-show-signature --decorate=full --format=%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s -s {sha}";
        }

        public Models.Commit GetResult()
        {
            var rs = ReadToEnd();
            return Parse(rs);
        }

        public async Task<Models.Commit> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return Parse(rs);
        }

        private Models.Commit Parse(Result rs)
        {
            if (!rs.IsSuccess || string.IsNullOrEmpty(rs.StdOut))
                return null;

            var commit = new Models.Commit();
            var lines = rs.StdOut.Split('\n');
            if (lines.Length < 8)
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

            return commit;
        }
    }
}
