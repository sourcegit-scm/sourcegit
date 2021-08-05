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
            public LineMode Mode { get; set; } = LineMode.None;
            public string Content { get; set; } = "";
            public string OldLine { get; set; } = "";
            public string NewLine { get; set; } = "";
            public List<HighlightRange> Highlights { get; set; } = new List<HighlightRange>();

            public bool IsContent {
                get {
                    return Mode == LineMode.Added
                        || Mode == LineMode.Deleted
                        || Mode == LineMode.Normal;
                }
            }

            public bool IsDifference {
                get {
                    return Mode == LineMode.Added
                        || Mode == LineMode.Deleted
                        || Mode == LineMode.None;
                }
            }

            public Line() {}

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
