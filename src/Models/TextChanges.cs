using System.Collections.Generic;

namespace SourceGit.Models {
    /// <summary>
    ///     Diff文本文件变化
    /// </summary>
    public class TextChanges {

        public enum LineMode {
            None,
            Normal,
            Indicator,
            Added,
            Deleted,
        }

        public class HighlightRange {
            public int Start { get; set; }
            public int Count { get; set; }
            public HighlightRange(int p, int n) { Start = p; Count = n; }
        }

        public class Line {
            public LineMode Mode = LineMode.Normal;
            public string Content = "";
            public string OldLine = "";
            public string NewLine = "";
            public List<HighlightRange> Highlights = new List<HighlightRange>();

            public Line(LineMode mode, string content, string oldLine, string newLine) {
                Mode = mode;
                Content = content;
                OldLine = oldLine;
                NewLine = newLine;
            }
        }

        public bool IsBinary = false;
        public List<Line> Lines = new List<Line>();
    }
}
