using System.Text.RegularExpressions;

namespace SourceGit.Git {

    /// <summary>
    ///     Changed file status.
    /// </summary>
    public class Change {
        private static readonly Regex FORMAT = new Regex(@"^(\s?[\w\?]{1,4})\s+(.+)$");

        /// <summary>
        ///     Status Code
        /// </summary>
        public enum Status {
            None,
            Modified,
            Added,
            Deleted,
            Renamed,
            Copied,
            Unmerged,
            Untracked,
        }

        /// <summary>
        ///     Index status
        /// </summary>
        public Status Index { get; set; }

        /// <summary>
        ///     Work tree status.
        /// </summary>
        public Status WorkTree { get; set; }

        /// <summary>
        ///     Current file path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        ///     Original file path before this revision.
        /// </summary>
        public string OriginalPath { get; set; }

        /// <summary>
        ///     Staged(added) in index?
        /// </summary>
        public bool IsAddedToIndex {
            get {
                if (Index == Status.None || Index == Status.Untracked) return false;
                return true;
            }
        }

        /// <summary>
        ///     Is conflict?
        /// </summary>
        public bool IsConflit {
            get {
                if (Index == Status.Unmerged || WorkTree == Status.Unmerged) return true;
                if (Index == Status.Added && WorkTree == Status.Added) return true;
                if (Index == Status.Deleted && WorkTree == Status.Deleted) return true;
                return false;
            }
        }

        /// <summary>
        ///     Parse change for `--name-status` data.
        /// </summary>
        /// <param name="data">Raw data.</param>
        /// <param name="fromCommit">Read from commit?</param>
        /// <returns>Parsed change instance.</returns>
        public static Change Parse(string data, bool fromCommit = false) {
            var match = FORMAT.Match(data);
            if (!match.Success) return null;

            var change = new Change() { Path = match.Groups[2].Value };
            var status = match.Groups[1].Value;

            if (fromCommit) {
                switch (status[0]) {
                case 'M': change.Set(Status.Modified); break;
                case 'A': change.Set(Status.Added); break;
                case 'D': change.Set(Status.Deleted); break;
                case 'R': change.Set(Status.Renamed); break;
                case 'C': change.Set(Status.Copied); break;
                default: return null;
                }
            } else {
                switch (status) {
                case " M": change.Set(Status.None, Status.Modified); break;
                case " A": change.Set(Status.None, Status.Added); break;
                case " D": change.Set(Status.None, Status.Deleted); break;
                case " R": change.Set(Status.None, Status.Renamed); break;
                case " C": change.Set(Status.None, Status.Copied); break;
                case "M": change.Set(Status.Modified, Status.None); break;
                case "MM": change.Set(Status.Modified, Status.Modified); break;
                case "MD": change.Set(Status.Modified, Status.Deleted); break;
                case "A": change.Set(Status.Added, Status.None); break;
                case "AM": change.Set(Status.Added, Status.Modified); break;
                case "AD": change.Set(Status.Added, Status.Deleted); break;
                case "D": change.Set(Status.Deleted, Status.None); break;
                case "R": change.Set(Status.Renamed, Status.None); break;
                case "RM": change.Set(Status.Renamed, Status.Modified); break;
                case "RD": change.Set(Status.Renamed, Status.Deleted); break;
                case "C": change.Set(Status.Copied, Status.None); break;
                case "CM": change.Set(Status.Copied, Status.Modified); break;
                case "CD": change.Set(Status.Copied, Status.Deleted); break;
                case "DR": change.Set(Status.Deleted, Status.Renamed); break;
                case "DC": change.Set(Status.Deleted, Status.Copied); break;
                case "DD": change.Set(Status.Deleted, Status.Deleted); break;
                case "AU": change.Set(Status.Added, Status.Unmerged); break;
                case "UD": change.Set(Status.Unmerged, Status.Deleted); break;
                case "UA": change.Set(Status.Unmerged, Status.Added); break;
                case "DU": change.Set(Status.Deleted, Status.Unmerged); break;
                case "AA": change.Set(Status.Added, Status.Added); break;
                case "UU": change.Set(Status.Unmerged, Status.Unmerged); break;
                case "??": change.Set(Status.Untracked, Status.Untracked); break;
                default: return null;
                }
            }

            if (change.Path[0] == '"') change.Path = change.Path.Substring(1, change.Path.Length - 2);
            if (!string.IsNullOrEmpty(change.OriginalPath) && change.OriginalPath[0] == '"') change.OriginalPath = change.OriginalPath.Substring(1, change.OriginalPath.Length - 2);
            return change;
        }

        private void Set(Status index, Status workTree = Status.None) {
            Index = index;
            WorkTree = workTree;

            if (index == Status.Renamed || workTree == Status.Renamed) {
                var idx = Path.IndexOf('\t');
                if (idx >= 0) {
                    OriginalPath = Path.Substring(0, idx);
                    Path = Path.Substring(idx + 1);
                } else {
                    idx = Path.IndexOf(" -> ");
                    if (idx > 0) {
                        OriginalPath = Path.Substring(0, idx);
                        Path = Path.Substring(idx + 4);
                    }
                }
            }
        }
    }
}
