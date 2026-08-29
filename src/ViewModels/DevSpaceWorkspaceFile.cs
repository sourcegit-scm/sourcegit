namespace SourceGit.ViewModels
{
    public sealed class DevSpaceWorkspaceFile
    {
        public string Path { get; }
        public string Content { get; }
        public string Message { get; }
        public bool HasContent => !string.IsNullOrEmpty(Content);

        public DevSpaceWorkspaceFile(string path, string content, string message = "")
        {
            Path = path;
            Content = content ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
