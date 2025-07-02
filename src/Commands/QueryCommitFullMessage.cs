using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCommitFullMessage : Command
    {
        public QueryCommitFullMessage(string repo, string sha)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"show --no-show-signature --format=%B -s {sha}";
        }

        public string Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess)
                return rs.StdOut.TrimEnd();
            return string.Empty;
        }

        public async Task<string> ResultAsync()
        {
            var rs = await ReadToEndAsync();
            if (rs.IsSuccess)
                return rs.StdOut.TrimEnd();
            return string.Empty;
        }
    }
}
