using System.Collections.Generic;

namespace SourceGit.Models {
    public enum TextDiffLineType {
        None,
        Normal,
        Indicator,
        Added,
        Deleted,
    }

    public class TextInlineRange {
        public int Start { get; set; }
        public int Count { get; set; }
        public TextInlineRange(int p, int n) { Start = p; Count = n; }
    }

    public class TextDiffLine {
        public TextDiffLineType Type { get; set; } = TextDiffLineType.None;
        public string Content { get; set; } = "";
        public string OldLine { get; set; } = "";
        public string NewLine { get; set; } = "";
        public List<TextInlineRange> Highlights { get; set; } = new List<TextInlineRange>();

        public TextDiffLine() { }
        public TextDiffLine(TextDiffLineType type, string content, string oldLine, string newLine) {
            Type = type;
            Content = content;
            OldLine = oldLine;
            NewLine = newLine;
        }
    }

    public class TextDiff {
        public string File { get; set; } = string.Empty;
        public List<TextDiffLine> Lines { get; set; } = new List<TextDiffLine>();
        public int MaxLineNumber = 0;
    }

    public class LFSDiff {
        public LFSObject Old { get; set; } = new LFSObject();
        public LFSObject New { get; set; } = new LFSObject();
    }

    public class BinaryDiff {
        public long OldSize { get; set; } = 0;
        public long NewSize { get; set; } = 0;
    }

    public class DiffResult {
        public bool IsBinary { get; set; } = false;
        public bool IsLFS { get; set; } = false;
        public TextDiff TextDiff { get; set; } = null;
        public LFSDiff LFSDiff { get; set; } = null;
    }
}
