using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public class Worktree : ObservableObject
    {
        public string Branch { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Head { get; set; } = string.Empty;
        public bool IsDetached { get; set; } = false;

        public bool IsLocked
        {
            get => _isLocked;
            set => SetProperty(ref _isLocked, value);
        }

        public string Name
        {
            get
            {
                if (IsDetached)
                    return $"detached HEAD at {Head.AsSpan(10)}";

                if (Branch.StartsWith("refs/heads/", StringComparison.Ordinal))
                    return Branch.Substring(11);

                if (Branch.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    return Branch.Substring(13);

                return Branch;
            }
        }

        private bool _isLocked = false;
    }
}
