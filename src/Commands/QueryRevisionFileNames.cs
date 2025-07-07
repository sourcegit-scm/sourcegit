using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryRevisionFileNames : Command
    {
        public QueryRevisionFileNames(string repo, string revision)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"ls-tree -r -z --name-only {revision}";
        }

        public async Task<List<string>> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return [];

            var lines = rs.StdOut.Split('\0', System.StringSplitOptions.RemoveEmptyEntries);
            return [.. lines];
        }
    }
}
