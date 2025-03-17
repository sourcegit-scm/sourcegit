namespace SourceGit.Models
{
    public interface IRepository
    {
        string FullPath { get; set; }
        string GitDirForWatcher { get; }

        void RefreshBranches();
        void RefreshWorktrees();
        void RefreshTags();
        void RefreshCommits();
        void RefreshSubmodules();
        void RefreshWorkingCopyChanges();
        void RefreshStashes();
    }
}
