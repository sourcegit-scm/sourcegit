using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    public class QueryStashChanges : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^(\s?[\w\?]{1,4})\s+(.+)$");

        public QueryStashChanges(string repo, string sha) {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff --name-status --pretty=format: {sha}^ {sha}";
        }

        public List<Models.Change> Result() {
            Exec();
            return _changes;
        }

        protected override void OnReadline(string line) {
            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;

            var change = new Models.Change() { Path = match.Groups[2].Value };
            var status = match.Groups[1].Value;

            switch (status[0]) {
            case 'M': change.Set(Models.ChangeState.Modified); _changes.Add(change); break;
            case 'A': change.Set(Models.ChangeState.Added); _changes.Add(change); break;
            case 'D': change.Set(Models.ChangeState.Deleted); _changes.Add(change); break;
            case 'R': change.Set(Models.ChangeState.Renamed); _changes.Add(change); break;
            case 'C': change.Set(Models.ChangeState.Copied); _changes.Add(change); break;
            }
        }

        private List<Models.Change> _changes = new List<Models.Change>();
    }
}
