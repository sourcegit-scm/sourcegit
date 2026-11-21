namespace SourceGit.Models
{
    public enum ChangeViewMode
    {
        List,
        Grid,
        Tree,
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

        public static ChangeState ParseState(char ch)
        {
            return ch switch
            {
                'M' => ChangeState.Modified,
                'T' => ChangeState.TypeChanged,
                'A' => ChangeState.Added,
                'D' => ChangeState.Deleted,
                'R' => ChangeState.Renamed,
                'C' => ChangeState.Copied,
                '?' => ChangeState.Untracked,
                _ => ChangeState.None,
            };
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
