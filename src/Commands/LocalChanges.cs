using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     取得本地工作副本变更
    /// </summary>
    public class LocalChanges : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^(\s?[\w\?]{1,4})\s+(.+)$");
        private List<Models.Change> changes = new List<Models.Change>();

        public LocalChanges(string path) {
            Cwd = path;
            Args = "status -uall --ignore-submodules=dirty --porcelain";
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

            switch (status) {
            case " M": change.Set(Models.Change.Status.None, Models.Change.Status.Modified); break;
            case " A": change.Set(Models.Change.Status.None, Models.Change.Status.Added); break;
            case " D": change.Set(Models.Change.Status.None, Models.Change.Status.Deleted); break;
            case " R": change.Set(Models.Change.Status.None, Models.Change.Status.Renamed); break;
            case " C": change.Set(Models.Change.Status.None, Models.Change.Status.Copied); break;
            case "M": change.Set(Models.Change.Status.Modified, Models.Change.Status.None); break;
            case "MM": change.Set(Models.Change.Status.Modified, Models.Change.Status.Modified); break;
            case "MD": change.Set(Models.Change.Status.Modified, Models.Change.Status.Deleted); break;
            case "A": change.Set(Models.Change.Status.Added, Models.Change.Status.None); break;
            case "AM": change.Set(Models.Change.Status.Added, Models.Change.Status.Modified); break;
            case "AD": change.Set(Models.Change.Status.Added, Models.Change.Status.Deleted); break;
            case "D": change.Set(Models.Change.Status.Deleted, Models.Change.Status.None); break;
            case "R": change.Set(Models.Change.Status.Renamed, Models.Change.Status.None); break;
            case "RM": change.Set(Models.Change.Status.Renamed, Models.Change.Status.Modified); break;
            case "RD": change.Set(Models.Change.Status.Renamed, Models.Change.Status.Deleted); break;
            case "C": change.Set(Models.Change.Status.Copied, Models.Change.Status.None); break;
            case "CM": change.Set(Models.Change.Status.Copied, Models.Change.Status.Modified); break;
            case "CD": change.Set(Models.Change.Status.Copied, Models.Change.Status.Deleted); break;
            case "DR": change.Set(Models.Change.Status.Deleted, Models.Change.Status.Renamed); break;
            case "DC": change.Set(Models.Change.Status.Deleted, Models.Change.Status.Copied); break;
            case "DD": change.Set(Models.Change.Status.Deleted, Models.Change.Status.Deleted); break;
            case "AU": change.Set(Models.Change.Status.Added, Models.Change.Status.Unmerged); break;
            case "UD": change.Set(Models.Change.Status.Unmerged, Models.Change.Status.Deleted); break;
            case "UA": change.Set(Models.Change.Status.Unmerged, Models.Change.Status.Added); break;
            case "DU": change.Set(Models.Change.Status.Deleted, Models.Change.Status.Unmerged); break;
            case "AA": change.Set(Models.Change.Status.Added, Models.Change.Status.Added); break;
            case "UU": change.Set(Models.Change.Status.Unmerged, Models.Change.Status.Unmerged); break;
            case "??": change.Set(Models.Change.Status.Untracked, Models.Change.Status.Untracked); break;
            default: return;
            }

            changes.Add(change);
        }
    }
}
