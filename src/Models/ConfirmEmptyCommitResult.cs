namespace SourceGit.Models
{
    public enum ConfirmEmptyCommitResult
    {
        Cancel = 0,
        StageSelectedAndCommit,
        StageAllAndCommit,
        CreateEmptyCommit,
    }
}
