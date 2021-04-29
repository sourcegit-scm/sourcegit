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

        public class Line {
            public LineMode Mode = LineMode.Normal;
            public string Content = "";
            public string OldLine = "";
            public string NewLine = "";

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
