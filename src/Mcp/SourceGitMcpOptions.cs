namespace SourceGit.Mcp
{
    public sealed class SourceGitMcpOptions
    {
        public const int DefaultPort = 53921;
        public const int DefaultMaxConcurrentToolCalls = 6;

        public int Port { get; set; } = DefaultPort;

        public bool ShareDevSpaceTerminalOutput { get; set; } = true;

        public string AuthToken { get; set; } = string.Empty;

        public int MaxConcurrentToolCalls { get; set; } = DefaultMaxConcurrentToolCalls;
    }
}
