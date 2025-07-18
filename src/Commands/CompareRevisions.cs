using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class CompareRevisions : Command
    {
        [GeneratedRegex(@"^([MADC])\s+(.+)$")]
        private static partial Regex REG_FORMAT();
        [GeneratedRegex(@"^R[0-9]{0,4}\s+(.+)$")]
        private static partial Regex REG_RENAME_FORMAT();

        public CompareRevisions(string repo, string start, string end)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status {based} {end}";
        }

        public CompareRevisions(string repo, string start, string end, string path)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status {based} {end} -- {path.Quoted()}";
        }

        public async Task<List<Models.Change>> ReadAsync()
        {
            var changes = new List<Models.Change>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return changes;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
                ParseLine(changes, line);

            changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
            return changes;
        }

        private void ParseLine(List<Models.Change> outs, string line)
        {
            var match = REG_FORMAT().Match(line);
            if (!match.Success)
            {
                match = REG_RENAME_FORMAT().Match(line);
                if (match.Success)
                {
                    var renamed = new Models.Change() { Path = match.Groups[1].Value };
                    renamed.Set(Models.ChangeState.Renamed);
                    outs.Add(renamed);
                }

                return;
            }

            var change = new Models.Change() { Path = match.Groups[2].Value };
            var status = match.Groups[1].Value;
            change.Set(Models.Change.ChangeStateFromCode(status[0]));
            outs.Add(change);
        }
    }
}
