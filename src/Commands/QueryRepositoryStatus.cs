using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryRepositoryStatus : Command
    {
        [GeneratedRegex(@"ahead\s(\d+)")]
        private static partial Regex REG_AHEAD();

        [GeneratedRegex(@"behind\s(\d+)")]
        private static partial Regex REG_BEHIND();

        public QueryRepositoryStatus(string repo)
        {
            WorkingDirectory = repo;
            RaiseError = false;
        }

        public async Task<Models.RepositoryStatus> GetResultAsync()
        {
            Args = "branch -l -v --format=\"%(refname:short)%00%(HEAD)%00%(upstream:track,nobracket)\"";
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            var status = new Models.RepositoryStatus();
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\0');
                if (parts.Length != 3 || !parts[1].Equals("*", StringComparison.Ordinal))
                    continue;

                status.CurrentBranch = parts[0];
                if (!string.IsNullOrEmpty(parts[2]))
                    ParseTrackStatus(status, parts[2]);
            }

            status.LocalChanges = await new CountLocalChanges(WorkingDirectory, true) { RaiseError = false }
                .GetResultAsync()
                .ConfigureAwait(false);

            return status;
        }

        private void ParseTrackStatus(Models.RepositoryStatus status, string input)
        {
            var aheadMatch = REG_AHEAD().Match(input);
            if (aheadMatch.Success)
                status.Ahead = int.Parse(aheadMatch.Groups[1].Value);

            var behindMatch = REG_BEHIND().Match(input);
            if (behindMatch.Success)
                status.Behind = int.Parse(behindMatch.Groups[1].Value);
        }
    }
}
