namespace SourceGit.Models
{
    public class Branch
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Head { get; set; }
        public bool IsLocal { get; set; }
        public bool IsCurrent { get; set; }
        public string Upstream { get; set; }
        public string UpstreamTrackStatus { get; set; }
        public string Remote { get; set; }
        public bool isHead { get; set; }
    }
}
