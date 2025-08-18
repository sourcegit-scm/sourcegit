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

        public string GetResult()
        {
            var rs = ReadToEnd();
            return rs.IsSuccess ? rs.StdOut.TrimEnd() : string.Empty;
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess ? rs.StdOut.TrimEnd() : string.Empty;
        }
    }
}
