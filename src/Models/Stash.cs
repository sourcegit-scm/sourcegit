namespace SourceGit.Models {
    /// <summary>
    ///     贮藏
    /// </summary>
    public class Stash {
        public string Name { get; set; } = "";
        public string SHA { get; set; } = "";
        public User Author { get; set; } = new User();
        public string Message { get; set; } = "";
    }
}
