namespace SourceGit.Models
{
    public enum SubmodulePointerState
    {
        Matches,
        Differs,
        Removed,
        Unavailable,
    }

    public class SubmodulePointerStatus
    {
        public string LocalHEAD { get; init; }
        public SubmodulePointerState State { get; init; }
    }
}
