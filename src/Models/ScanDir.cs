namespace SourceGit.Models
{
    public record ScanDir(string path, string desc)
    {
        public string Path { get; set; } = path;
        public string Desc { get; set; } = desc;
    }
}
