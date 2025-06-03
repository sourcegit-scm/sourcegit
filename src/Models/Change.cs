using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum ChangeViewMode
    {
        List,
        Grid,
        Tree,
    }

    [Flags]
    public enum ChangeState
    {
        None = 0,
        Modified = 1 << 0,
        TypeChanged = 1 << 1,
        Added = 1 << 2,
        Deleted = 1 << 3,
        Renamed = 1 << 4,
        Copied = 1 << 5,
        Untracked = 1 << 6,
        Conflicted = 1 << 7,
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

        public string WorkTreeDesc => TYPE_DESCS[GetPrimaryState(WorkTree)];
        public string IndexDesc => TYPE_DESCS[GetPrimaryState(Index)];

        public void Set(ChangeState index, ChangeState workTree = ChangeState.None)
        {
            Index = index;
            WorkTree = workTree;

            if (index == ChangeState.Renamed || workTree == ChangeState.Renamed)
            {
                var idx = Path.IndexOf('\t', StringComparison.Ordinal);
                if (idx >= 0)
                {
                    OriginalPath = Path.Substring(0, idx);
                    Path = Path.Substring(idx + 1);
                }
                else
                {
                    idx = Path.IndexOf(" -> ", StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        OriginalPath = Path.Substring(0, idx);
                        Path = Path.Substring(idx + 4);
                    }
                }
            }

            if (Path[0] == '"')
                Path = Path.Substring(1, Path.Length - 2);

            if (!string.IsNullOrEmpty(OriginalPath) && OriginalPath[0] == '"')
                OriginalPath = OriginalPath.Substring(1, OriginalPath.Length - 2);
        }

        public static ChangeState GetPrimaryState(ChangeState state)
        {
            if (state == ChangeState.None)
                return ChangeState.None;
            if ((state & ChangeState.Conflicted) != 0)
                return ChangeState.Conflicted;
            if ((state & ChangeState.Untracked) != 0)
                return ChangeState.Untracked;
            if ((state & ChangeState.Renamed) != 0)
                return ChangeState.Renamed;
            if ((state & ChangeState.Copied) != 0)
                return ChangeState.Copied;
            if ((state & ChangeState.Deleted) != 0)
                return ChangeState.Deleted;
            if ((state & ChangeState.Added) != 0)
                return ChangeState.Added;
            if ((state & ChangeState.TypeChanged) != 0)
                return ChangeState.TypeChanged;
            if ((state & ChangeState.Modified) != 0)
                return ChangeState.Modified;

            return ChangeState.None;
        }

        private static readonly Dictionary<ChangeState, string> TYPE_DESCS = new Dictionary<ChangeState, string>
        {
            { ChangeState.None, "Unknown" },
            { ChangeState.Modified, "Modified" },
            { ChangeState.TypeChanged, "Type Changed" },
            { ChangeState.Added, "Added" },
            { ChangeState.Deleted, "Deleted" },
            { ChangeState.Renamed, "Renamed" },
            { ChangeState.Copied, "Copied" },
            { ChangeState.Untracked, "Untracked" },
            { ChangeState.Conflicted, "Conflict" }
        };

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
