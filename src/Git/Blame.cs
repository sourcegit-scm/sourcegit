using System.Collections.Generic;

namespace SourceGit.Git {

    /// <summary>
    ///     Blame
    /// </summary>
    public class Blame {

        /// <summary>
        ///     Block content.
        /// </summary>
        public class Block {
            public string CommitSHA { get; set; }
            public string Author { get; set; }
            public string Time { get; set; }
            public string Content { get; set; }
        }

        /// <summary>
        ///     Blocks
        /// </summary>
        public List<Block> Blocks { get; set; } = new List<Block>();

        /// <summary>
        ///     Is binary file?
        /// </summary>
        public bool IsBinary { get; set; } = false;

        /// <summary>
        ///     Line count.
        /// </summary>
        public int LineCount { get; set; } = 0;
    }
}
