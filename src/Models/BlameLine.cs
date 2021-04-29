namespace SourceGit.Models {
    /// <summary>
    ///     追溯中的行信息
    /// </summary>
    public class BlameLine {
        public string LineNumber { get; set; }
        public string CommitSHA { get; set; }
        public string Author { get; set; }
        public string Time { get; set; }
        public string Content { get; set; }
    }
}
