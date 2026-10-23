using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryRepositoryRootPath : Command
    {
        public QueryRepositoryRootPath(string path)
        {
            WorkingDirectory = path;
            Args = "rev-parse --show-toplevel";
        }

        public Result GetResult()
        {
            return Translate(ReadToEnd());
        }

        public async Task<Result> GetResultAsync()
        {
            return Translate(await ReadToEndAsync().ConfigureAwait(false));
        }

        private Result Translate(Result rs)
        {
            if (rs.IsSuccess)
                rs.StdOut = FromGitPath(rs.StdOut.Trim());

            return rs;
        }
    }
}
