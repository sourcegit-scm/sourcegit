namespace SourceGit.Models {

    /// <summary>
    ///     Git变更
    /// </summary>
    public class Change {

        /// <summary>
        ///     显示模式
        /// </summary>
        public enum DisplayMode {
            Tree,
            List,
            Grid,
        }

        /// <summary>
        ///     变更状态码
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

        public Status Index { get; set; }
        public Status WorkTree { get; set; } = Status.None;
        public string Path { get; set; } = "";
        public string OriginalPath { get; set; } = "";

        public bool IsAddedToIndex {
            get {
                if (Index == Status.None || Index == Status.Untracked) return false;
                return true;
            }
        }

        public bool IsConflit {
            get {
                if (Index == Status.Unmerged || WorkTree == Status.Unmerged) return true;
                if (Index == Status.Added && WorkTree == Status.Added) return true;
                if (Index == Status.Deleted && WorkTree == Status.Deleted) return true;
                return false;
            }
        }

        public void Set(Status index, Status workTree = Status.None) {
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

            if (Path[0] == '"') Path = Path.Substring(1, Path.Length - 2);
            if (!string.IsNullOrEmpty(OriginalPath) && OriginalPath[0] == '"') OriginalPath = OriginalPath.Substring(1, OriginalPath.Length - 2);
        }
    }
}
