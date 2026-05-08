using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryRepositoryStatus : Command
    {
        [GeneratedRegex(@"\+(\d+) \-(\d+)")]
        private static partial Regex REG_BRANCH_AB();

        public QueryRepositoryStatus(string repo)
        {
            WorkingDirectory = repo;
            RaiseError = false;
        }

        public async Task<Models.RepositoryStatus> GetResultAsync()
        {
            Args = "status --porcelain=v2 -b";
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            var status = new Models.RepositoryStatus();
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var count = lines.Length;
            if (count < 2)
                return null;

            var sha1 = lines[0].Substring(13).Trim(); // Remove "# branch.oid " prefix
            var head = lines[1].Substring(14).Trim(); // Remove "# branch.head " prefix

            if (head.Equals("(detached)", StringComparison.Ordinal))
                status.CurrentBranch = sha1.Length > 10 ? $"({sha1.Substring(0, 10)})" : "-";
            else
                status.CurrentBranch = head;

            if (count == 4 && lines[3].StartsWith("# branch.ab ", StringComparison.Ordinal))
                ParseTrackStatus(status, lines[3].Substring(12).Trim());

            status.LocalChanges = await new CountLocalChanges(WorkingDirectory, true) { RaiseError = false }
                .GetResultAsync()
                .ConfigureAwait(false);

            return status;
        }

        private void ParseTrackStatus(Models.RepositoryStatus status, string input)
        {
            var match = REG_BRANCH_AB().Match(input);
            if (match.Success)
            {
                status.Ahead = int.Parse(match.Groups[1].Value);
                status.Behind = int.Parse(match.Groups[2].Value);
            }
        }
    }
}
