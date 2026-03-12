namespace SourceGit.Models
{
    public class FileVersion
    {
        public string SHA { get; set; } = string.Empty;
        public bool HasParent { get; set; } = false;
        public User Author { get; set; } = User.Invalid;
        public ulong AuthorTime { get; set; } = 0;
        public string Subject { get; set; } = string.Empty;
        public Change Change { get; set; } = new();
        public string Path => Change.Path;
        public string OriginalPath => Change.OriginalPath;
    }
}
