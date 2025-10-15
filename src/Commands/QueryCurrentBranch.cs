using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCurrentBranch : Command
    {
        public QueryCurrentBranch(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "branch --show-current";
        }

        public string GetResult()
        {
            return ReadToEnd().StdOut.Trim();
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.StdOut.Trim();
        }
    }
}
