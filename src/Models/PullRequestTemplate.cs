namespace SourceGit.Models
{
    public class PullRequestTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
    }
}
