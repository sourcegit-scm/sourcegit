using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Worktree : ObservableObject
    {
        public Models.Worktree Backend { get; private set; }
        public bool IsMain { get; private set; }
        public bool IsCurrent { get; private set; }
        public string DisplayPath { get; private set; }
        public string Name { get; private set; }
        public string Branch { get; private set; }
        public int Depth { get; private set; }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetProperty(ref _isLocked, value);
        }

        public string FullPath => Backend.FullPath;
        public string Head => Backend.Head;

        public Worktree(DirectoryInfo repo, Models.Worktree wt, bool isMain)
        {
            Backend = wt;
            IsMain = isMain;
            IsCurrent = IsCurrentWorktree(repo, wt);
            DisplayPath = IsCurrent ? string.Empty : Path.GetRelativePath(repo.FullName, wt.FullPath);
            Name = GenerateName();
            Branch = GenerateBranchName();
            Depth = isMain ? 0 : 1;
            IsLocked = wt.IsLocked;
        }

        public static List<Worktree> Build(string repo, List<Models.Worktree> worktrees)
        {
            if (worktrees is not { Count: > 1 })
                return [];

            var repoDir = new DirectoryInfo(repo);
            var nodes = new List<Worktree>();
            nodes.Add(new(repoDir, worktrees[0], true));
            for (int i = 1; i < worktrees.Count; i++)
                nodes.Add(new(repoDir, worktrees[i], false));

            return nodes;
        }

        public bool IsAttachedTo(Models.Branch branch)
        {
            if (string.IsNullOrEmpty(branch.WorktreePath))
                return false;

            var wtDir = new DirectoryInfo(Backend.FullPath);
            var test = new DirectoryInfo(branch.WorktreePath);
            return test.FullName.Equals(wtDir.FullName, StringComparison.Ordinal);
        }

        private bool IsCurrentWorktree(DirectoryInfo repo, Models.Worktree wt)
        {
            var wtDir = new DirectoryInfo(wt.FullPath);
            return wtDir.FullName.Equals(repo.FullName, StringComparison.Ordinal);
        }

        private string GenerateName()
        {
            if (IsMain)
                return Path.GetFileName(Backend.FullPath);

            if (Backend.IsDetached)
                return $"detached HEAD at {Backend.Head.AsSpan(10)}";

            var b = Backend.Branch;

            if (b.StartsWith("refs/heads/", StringComparison.Ordinal))
                return b.Substring(11);

            if (b.StartsWith("refs/remotes/", StringComparison.Ordinal))
                return b.Substring(13);

            return b;
        }

        private string GenerateBranchName()
        {
            if (Backend.IsBare)
                return "-- (default)";

            if (Backend.IsDetached)
                return "-- (detached)";

            if (string.IsNullOrEmpty(Backend.Branch))
                return "-- (unknown)";

            var b = Backend.Branch;

            if (b.StartsWith("refs/heads/", StringComparison.Ordinal))
                return b.Substring(11);

            if (b.StartsWith("refs/remotes/", StringComparison.Ordinal))
                return b.Substring(13);

            return b;
        }

        private bool _isLocked = false;
    }
}
