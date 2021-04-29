namespace SourceGit.Models {

    /// <summary>
    ///     修饰类型
    /// </summary>
    public enum DecoratorType {
        None,
        CurrentBranchHead,
        LocalBranchHead,
        RemoteBranchHead,
        Tag,
    }

    /// <summary>
    ///     提交的附加修饰
    /// </summary>
    public class Decorator {
        public DecoratorType Type { get; set; } = DecoratorType.None;
        public string Name { get; set; } = "";
    }
}
