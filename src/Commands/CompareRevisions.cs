using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            Args = $"diff --name-status {based} {end} -- \"{path}\"";
        }

        public List<Models.Change> Result()
        {
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return _changes;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
                ParseLine(line);

            _changes.Sort((l, r) => string.Compare(l.Path, r.Path, StringComparison.Ordinal));
            return _changes;
        }

        private void ParseLine(string line)
        {
            var match = REG_FORMAT().Match(line);
            if (!match.Success)
            {
                match = REG_RENAME_FORMAT().Match(line);
                if (match.Success)
                {
                    var renamed = new Models.Change() { Path = match.Groups[1].Value };
                    renamed.Set(Models.ChangeState.Renamed);
                    _changes.Add(renamed);
                }

                return;
            }

            var change = new Models.Change() { Path = match.Groups[2].Value };
            var status = match.Groups[1].Value;

            switch (status[0])
            {
                case 'M':
                    change.Set(Models.ChangeState.Modified);
                    _changes.Add(change);
                    break;
                case 'A':
                    change.Set(Models.ChangeState.Added);
                    _changes.Add(change);
                    break;
                case 'D':
                    change.Set(Models.ChangeState.Deleted);
                    _changes.Add(change);
                    break;
                case 'C':
                    change.Set(Models.ChangeState.Copied);
                    _changes.Add(change);
                    break;
            }
        }

        private readonly List<Models.Change> _changes = new List<Models.Change>();
    }
}
