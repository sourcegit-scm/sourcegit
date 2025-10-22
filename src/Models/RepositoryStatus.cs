namespace SourceGit.Models
{
    public class RepositoryStatus
    {
        public string CurrentBranch { get; set; } = string.Empty;
        public int Ahead { get; set; } = 0;
        public int Behind { get; set; } = 0;
        public int LocalChanges { get; set; } = 0;

        public bool IsTrackingStatusVisible
        {
            get
            {
                return Ahead > 0 || Behind > 0;
            }
        }

        public string TrackingDescription
        {
            get
            {
                if (Ahead > 0)
                    return Behind > 0 ? $"{Ahead}↑ {Behind}↓" : $"{Ahead}↑";

                return Behind > 0 ? $"{Behind}↓" : string.Empty;
            }
        }
    }
}
