namespace SourceGit.Models
{
    public class Worktree
    {
        public string Branch { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Head { get; set; } = string.Empty;
        public bool IsBare { get; set; } = false;
        public bool IsDetached { get; set; } = false;
        public bool IsLocked { get; set; } = false;
    }
}
