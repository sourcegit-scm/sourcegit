using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryCommitStatistic : Command
    {
        [GeneratedRegex(@"(\d+) files? changed(?:, (\d+) insertions?\(\+\))?(?:, (\d+) deletions?\(-\))?")]
        private static partial Regex REG_SHORTSTAT();

        public QueryCommitStatistic(string repo, string parentRevision, string targetRevision)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"--no-optional-locks diff --shortstat --no-color {parentRevision} {targetRevision}";
        }

        public async Task<(int files, int added, int deleted)> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess || string.IsNullOrWhiteSpace(rs.StdOut))
                return (0, 0, 0);

            var files = 0;
            var added = 0;
            var deleted = 0;

            var match = REG_SHORTSTAT().Match(rs.StdOut.Trim());
            if (match.Success)
            {
                if (match.Groups[1].Success)
                    files = int.Parse(match.Groups[1].Value);
                if (match.Groups[2].Success)
                    added = int.Parse(match.Groups[2].Value);
                if (match.Groups[3].Success)
                    deleted = int.Parse(match.Groups[3].Value);
            }

            return (files, added, deleted);
        }
    }
}
