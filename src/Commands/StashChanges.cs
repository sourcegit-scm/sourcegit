using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     查看Stash中的修改
    /// </summary>
    public class StashChanges : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^(\s?[\w\?]{1,4})\s+(.+)$");
        private List<Models.Change> changes = new List<Models.Change>();

        public StashChanges(string repo, string sha) {
            Cwd = repo;
            Args = $"diff --name-status --pretty=format: {sha}^ {sha}";
        }

        public List<Models.Change> Result() {
            Exec();
            return changes;
        }

        public override void OnReadline(string line) {
            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;

            var change = new Models.Change() { Path = match.Groups[2].Value };
            var status = match.Groups[1].Value;

            switch (status[0]) {
            case 'M': change.Set(Models.Change.Status.Modified); changes.Add(change); break;
            case 'A': change.Set(Models.Change.Status.Added); changes.Add(change); break;
            case 'D': change.Set(Models.Change.Status.Deleted); changes.Add(change); break;
            case 'R': change.Set(Models.Change.Status.Renamed); changes.Add(change); break;
            case 'C': change.Set(Models.Change.Status.Copied); changes.Add(change); break;
            }
        }
    }
}
