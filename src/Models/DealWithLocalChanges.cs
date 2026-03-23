namespace SourceGit.Models
{
    public enum DealWithLocalChanges
    {
        DoNothing = 0,
        StashAndReapply,
        Discard,
    }
}
