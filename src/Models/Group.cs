namespace SourceGit.Models {

    /// <summary>
    ///     仓库列表分组
    /// </summary>
    public class Group {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Parent { get; set; } = "";
        public bool IsExpanded { get; set; } = false;
    }
}
