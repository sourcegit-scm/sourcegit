namespace SourceGit.Models
{
    public enum SubmoduleStatus
    {
        Normal = 0,
        NotInited,
        RevisionChanged,
        Unmerged,
        Modified,
    }

    public class Submodule
    {
        public string Path { get; set; } = string.Empty;
        public string SHA { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public SubmoduleStatus Status { get; set; } = SubmoduleStatus.Normal;
        public bool IsDirty => Status > SubmoduleStatus.NotInited;
    }
}
