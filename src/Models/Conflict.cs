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

    public enum ConflictMarkerType
    {
        Start,      // <<<<<<<
        Base,       // ||||||| (diff3 style)
        Separator,  // =======
        End,        // >>>>>>>
    }

    public enum ConflictPanelType
    {
        Mine,
        Theirs,
        Result
    }

    public enum ConflictLineState
    {
        Normal,
        ConflictBlockStart,
        ConflictBlock,
        ConflictBlockEnd,
        ResolvedBlockStart,
        ResolvedBlock,
        ResolvedBlockEnd,
    }

    public record ConflictSelectedChunk(
        double Y,
        double Height,
        int ConflictIndex,
        ConflictPanelType Panel,
        bool IsResolved
    );

    public class ConflictMarkerInfo
    {
        public int LineNumber { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public ConflictMarkerType Type { get; set; }
    }

    public class ConflictRegion
    {
        public int StartLineInOriginal { get; set; }
        public int EndLineInOriginal { get; set; }

        public string StartMarker { get; set; } = "<<<<<<<";
        public string SeparatorMarker { get; set; } = "=======";
        public string EndMarker { get; set; } = ">>>>>>>";

        public List<string> OursContent { get; set; } = new();
        public List<string> TheirsContent { get; set; } = new();

        public bool IsResolved { get; set; } = false;
        public ConflictResolution ResolutionType { get; set; } = ConflictResolution.None;
    }
}
