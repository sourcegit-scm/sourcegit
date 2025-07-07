using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryRevisionByRefName : Command
    {
        public QueryRevisionByRefName(string repo, string refname)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"rev-parse {refname}";
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess && !string.IsNullOrEmpty(rs.StdOut))
                return rs.StdOut.Trim();

            return null;
        }
    }
}
