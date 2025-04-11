namespace SourceGit.Models
{
    public interface IRepository
    {
        void RefreshBranches();
        void RefreshWorktrees();
        void RefreshTags();
        void RefreshCommits();
        void RefreshSubmodules();
        void RefreshWorkingCopyChanges();
        void RefreshStashes();
    }
}
