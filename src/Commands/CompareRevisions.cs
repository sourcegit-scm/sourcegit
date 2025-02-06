using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class CompareRevisions : Command
    {
        [GeneratedRegex(@"^(\s?[\w\?]{1,4})\s+(.+)$")]
        private static partial Regex REG_FORMAT();

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
            Args = $"diff --name-status {based} {end} -- {path}";
        }

        public List<Models.Change> Result()
        {
            Exec();
            _changes.Sort((l, r) => string.Compare(l.Path, r.Path, StringComparison.Ordinal));
            return _changes;
        }

        protected override void OnReadline(string line)
        {
            var match = REG_FORMAT().Match(line);
            if (!match.Success)
                return;

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
                case 'R':
                    change.Set(Models.ChangeState.Renamed);
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
