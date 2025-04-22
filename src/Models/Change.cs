using System;

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
        Unmerged,
        Untracked
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

        public bool IsConflict
        {
            get
            {
                if (Index == ChangeState.Unmerged || WorkTree == ChangeState.Unmerged)
                    return true;
                if (Index == ChangeState.Added && WorkTree == ChangeState.Added)
                    return true;
                if (Index == ChangeState.Deleted && WorkTree == ChangeState.Deleted)
                    return true;
                return false;
            }
        }

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
    }
}
