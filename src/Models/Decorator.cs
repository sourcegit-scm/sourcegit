namespace SourceGit.Models
{
    public enum DecoratorType
    {
        None,
        CurrentBranchHead,
        LocalBranchHead,
        CurrentCommitHead,
        RemoteBranchHead,
        Tag,
    }

    public class Decorator
    {
        public DecoratorType Type { get; set; } = DecoratorType.None;
        public string Name { get; set; } = "";
        public bool IsTag => Type == DecoratorType.Tag;
    }
}
