namespace SourceGit.ViewModels
{
    public class FileContent(string path, object content)
    {
        public string Path { get; set; } = path;
        public object Content { get; set; } = content;
    }
}
