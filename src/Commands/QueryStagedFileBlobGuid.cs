using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryStagedFileBlobGuid : Command
    {
        [GeneratedRegex(@"^\d+\s+([0-9a-f]+)\s+.*$")]
        private static partial Regex REG_FORMAT();

        public QueryStagedFileBlobGuid(string repo, string file)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"ls-files -s -- {file.Quoted()}";
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var match = REG_FORMAT().Match(rs.StdOut.Trim());
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
