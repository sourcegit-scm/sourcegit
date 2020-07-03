namespace SourceGit.Git {

    /// <summary>
    ///     Decorator type.
    /// </summary>
    public enum DecoratorType {
        None,
        CurrentBranchHead,
        LocalBranchHead,
        RemoteBranchHead,
        Tag,
    }

    /// <summary>
    ///     Commit decorator.
    /// </summary>
    public class Decorator {
        public DecoratorType Type { get; set; }
        public string Name { get; set; }
    }
}
