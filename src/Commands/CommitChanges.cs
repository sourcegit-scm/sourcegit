using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {

    /// <summary>
    ///     取得一个提交的变更列表
    /// </summary>
    public class CommitChanges : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^(\s?[\w\?]{1,4})\s+(.+)$");
        private List<Models.Change> changes = new List<Models.Change>();

        public CommitChanges(string cwd, string commit) {
            Cwd = cwd;
            Args = $"show --name-status {commit}";
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
