using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum ConflictResolution
    {
        None,
        UseOurs,
        UseTheirs,
        UseBothMineFirst,
        UseBothTheirsFirst,
    }

    public class MergeConflictRegion
    {
        public int StartLine { get; set; } = 0;
        public int EndLine { get; set; } = 0;
        public bool IsConflict { get; set; } = false;
        public ConflictResolution Resolution { get; set; } = ConflictResolution.None;

        public string BaseContent { get; set; } = string.Empty;
        public string OursContent { get; set; } = string.Empty;
        public string TheirsContent { get; set; } = string.Empty;
    }

    public class MergeConflictDocument
    {
        public string BaseContent { get; set; } = string.Empty;
        public string OursContent { get; set; } = string.Empty;
        public string TheirsContent { get; set; } = string.Empty;
        public string ResultContent { get; set; } = string.Empty;

        public List<MergeConflictRegion> Regions { get; set; } = new List<MergeConflictRegion>();

        public int UnresolvedConflictCount
        {
            get
            {
                int count = 0;
                foreach (var region in Regions)
                {
                    if (region.IsConflict && region.Resolution == ConflictResolution.None)
                        count++;
                }
                return count;
            }
        }

        public bool HasUnresolvedConflicts => UnresolvedConflictCount > 0;
    }

    public class ConflictMarkerInfo
    {
        public int LineNumber { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public ConflictMarkerType Type { get; set; }
    }

    public enum ConflictMarkerType
    {
        Start,      // <<<<<<<
        Base,       // ||||||| (diff3 style)
        Separator,  // =======
        End,        // >>>>>>>
    }
}
