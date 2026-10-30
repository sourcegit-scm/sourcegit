using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum ConflictFileState
    {
        Unknown,
        UnmergedText,
        UnmergedBinary,
        Resolved
    }

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
        Edited,
    }

    public record struct ConflictLineDecoration(
        ConflictLineType Type,
        bool InRegion,
        bool IsResolved,
        bool IsFirst,
        bool IsLast
    );

    public record ConflictPick(ConflictPanelType Side, int Index);

    public class ConflictLine
    {
        public ConflictLineType Type { get; set; } = ConflictLineType.None;
        public string Content { get; set; } = string.Empty;
        public string LineNumber { get; set; } = string.Empty;
        public int RegionIndex { get; set; } = -1;
        public int IndexInSide { get; set; } = -1;

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
        public int OursFirst { get; set; }
        public int TheirsFirst { get; set; }
        public bool IsAtEnd { get; set; }

        public string StartMarker { get; set; } = "<<<<<<<";
        public string SeparatorMarker { get; set; } = "=======";
        public string EndMarker { get; set; } = ">>>>>>>";

        public List<string> OursContent { get; set; } = new();
        public List<string> TheirsContent { get; set; } = new();

        // An empty side still takes one (placeholder) row in its panel, so that it can be picked.
        public int OursRowCount => Math.Max(OursContent.Count, 1);
        public int TheirsRowCount => Math.Max(TheirsContent.Count, 1);

        public List<ConflictPick> Picks { get; set; } = new();
        public bool HasChoice { get; set; } = false;
        public bool IsCustom { get; set; } = false;
        public bool IsResolved { get; set; } = false;
    }
}
