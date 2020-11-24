using System.Collections.Generic;

namespace SourceGit.Git {

    /// <summary>
    ///     Blame
    /// </summary>
    public class Blame {

        /// <summary>
        ///     Line content.
        /// </summary>
        public class Line {
            public string CommitSHA { get; set; }
            public string Author { get; set; }
            public string Time { get; set; }
            public string Content { get; set; }
        }

        /// <summary>
        ///     Lines
        /// </summary>
        public List<Line> Lines { get; set; } = new List<Line>();

        /// <summary>
        ///     Is binary file?
        /// </summary>
        public bool IsBinary { get; set; } = false;
    }
}
