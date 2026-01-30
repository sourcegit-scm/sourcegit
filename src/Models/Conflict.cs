using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum ConflictPanelType
    {
        Ours,
        Theirs,
        Result
    }

    public enum ConflictResolution
    {
        None,
        UseOurs,
        UseTheirs,
        UseBothMineFirst,
        UseBothTheirsFirst,
    }

    public enum ConflictLineType
    {
        None,
        Common,
        Marker,
        Ours,
        Theirs,
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

    public class ConflictLine
    {
        public ConflictLineType Type { get; set; } = ConflictLineType.None;
        public string Content { get; set; } = string.Empty;
        public string LineNumber { get; set; } = string.Empty;

        public ConflictLine()
        {
        }
        public ConflictLine(ConflictLineType type, string content)
        {
            Type = type;
            Content = content;
        }
        public ConflictLine(ConflictLineType type, string content, int lineNumber)
        {
            Type = type;
            Content = content;
            LineNumber = lineNumber.ToString();
        }
    }

    public record ConflictSelectedChunk(
        double Y,
        double Height,
        int ConflictIndex,
        ConflictPanelType Panel,
        bool IsResolved
    );

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
