namespace SourceGit.Models
{
    public interface IRepository
    {
        string FullPath { get; set; }
        string GitDir { get; set; }

        void RefreshBranches();
        void RefreshWorktrees();
        void RefreshTags();
        void RefreshCommits();
        void RefreshSubmodules();
        void RefreshWorkingCopyChanges();
        void RefreshStashes();
    }
}
