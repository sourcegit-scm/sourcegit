namespace SourceGit.Models {
    /// <summary>
    ///     提交中元素类型
    /// </summary>
    public enum ObjectType {
        None,
        Blob,
        Tree,
        Tag,
        Commit,
    }

    /// <summary>
    ///     Git提交中的元素
    /// </summary>
    public class Object {
        public string SHA { get; set; }
        public ObjectType Type { get; set; }
        public string Path { get; set; }
    }
}
