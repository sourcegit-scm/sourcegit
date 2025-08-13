namespace SourceGit.Models
{
    public enum ChangeViewMode
    {
        List,
        Grid,
        Tree,
    }

    public enum ChangeSortMode
    {
        Path,
        Status,
    }

    public enum ChangeState
    {
        None,
        Modified,
        TypeChanged,
        Added,
        Deleted,
        Renamed,
        Copied,
        Untracked,
        Conflicted,
    }

    public enum ConflictReason
    {
        None,
        BothDeleted,
        AddedByUs,
        DeletedByThem,
        AddedByThem,
        DeletedByUs,
        BothAdded,
        BothModified,
    }

    public class ChangeDataForAmend
    {
        public string FileMode { get; set; } = "";
        public string ObjectHash { get; set; } = "";
        public string ParentSHA { get; set; } = "";
    }

    public class Change
    {
        public ChangeState Index { get; set; } = ChangeState.None;
        public ChangeState WorkTree { get; set; } = ChangeState.None;
        public string Path { get; set; } = "";
        public string OriginalPath { get; set; } = "";
        public ChangeDataForAmend DataForAmend { get; set; } = null;
        public ConflictReason ConflictReason { get; set; } = ConflictReason.None;

        public bool IsConflicted => WorkTree == ChangeState.Conflicted;
        public string ConflictMarker => CONFLICT_MARKERS[(int)ConflictReason];
        public string ConflictDesc => CONFLICT_DESCS[(int)ConflictReason];

        public string WorkTreeDesc => TYPE_DESCS[(int)WorkTree];
        public string IndexDesc => TYPE_DESCS[(int)Index];

        public void Set(ChangeState index, ChangeState workTree = ChangeState.None)
        {
            Index = index;
            WorkTree = workTree;

            if (index == ChangeState.Renamed || workTree == ChangeState.Renamed)
            {
                var parts = Path.Split('\t', 2);
                if (parts.Length < 2)
                    parts = Path.Split(" -> ", 2);
                if (parts.Length == 2)
                {
                    OriginalPath = parts[0];
                    Path = parts[1];
                }
            }

            if (Path[0] == '"')
                Path = Path.Substring(1, Path.Length - 2);

            if (!string.IsNullOrEmpty(OriginalPath) && OriginalPath[0] == '"')
                OriginalPath = OriginalPath.Substring(1, OriginalPath.Length - 2);
        }

        /// <summary>
        /// Gets the sort priority for a change based on its status, used for sorting changes by status.
        /// Lower numbers indicate higher priority (appear first in sorted lists).
        /// </summary>
        /// <param name="change">The change object to get priority for</param>
        /// <param name="isUnstagedContext">True if sorting in unstaged context, false for staged context</param>
        /// <returns>Priority value where lower numbers appear first</returns>
        public static int GetStatusSortPriority(Change change, bool isUnstagedContext)
        {
            if (change == null) return int.MaxValue;

            if (isUnstagedContext)
            {
                // For unstaged context, only consider WorkTree state
                return change.WorkTree switch
                {
                    ChangeState.Conflicted => 1,   // Conflicts first - most urgent
                    ChangeState.Modified => 2,
                    ChangeState.TypeChanged => 3,
                    ChangeState.Deleted => 4,      // Missing files
                    ChangeState.Renamed => 5,
                    ChangeState.Copied => 6,
                    ChangeState.Untracked => 7,    // New files last
                    _ => 10
                };
            }
            else
            {
                // For staged context, only consider Index state
                return change.Index switch
                {
                    ChangeState.Modified => 1,
                    ChangeState.TypeChanged => 2,
                    ChangeState.Renamed => 3,
                    ChangeState.Copied => 4,
                    ChangeState.Added => 5,
                    ChangeState.Deleted => 6,
                    _ => 10
                };
            }
        }

        private static readonly string[] TYPE_DESCS =
        [
            "Unknown",
            "Modified",
            "Type Changed",
            "Added",
            "Deleted",
            "Renamed",
            "Copied",
            "Untracked",
            "Conflict"
        ];
        private static readonly string[] CONFLICT_MARKERS =
        [
            string.Empty,
            "DD",
            "AU",
            "UD",
            "UA",
            "DU",
            "AA",
            "UU"
        ];
        private static readonly string[] CONFLICT_DESCS =
        [
            string.Empty,
            "Both deleted",
            "Added by us",
            "Deleted by them",
            "Added by them",
            "Deleted by us",
            "Both added",
            "Both modified"
        ];
    }
}
