namespace SourceGit.Models
{
    public class ReflogEntry
    {
        public string SHA { get; set; } = string.Empty;
        public string Selector { get; set; } = string.Empty;
        public ulong Time { get; set; } = 0;
        public string Message { get; set; } = string.Empty;
    }
}
