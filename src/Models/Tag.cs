namespace SourceGit.Models {
    /// <summary>
    ///     标签
    /// </summary>
    public class Tag {
        public string Name { get; set; }
        public string SHA { get; set; }
        public bool IsFiltered { get; set; }
    }
}
