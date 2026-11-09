using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryUpstreamOfCurrentBranch : Command
    {
        public QueryUpstreamOfCurrentBranch(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "rev-parse --symbolic-full-name \"@{upstream}\"";
            RaiseError = false;
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return string.Empty;

            return rs.StdOut.Trim();
        }
    }
}
