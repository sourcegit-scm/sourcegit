namespace SourceGit.Models {
    /// <summary>
    ///     子树
    /// </summary>
    public class SubTree {
        public string Prefix { get; set; }
        public string Remote { get; set; }
        public string Branch { get; set; } = "master";
    }
}
