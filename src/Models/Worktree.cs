using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public class Worktree : ObservableObject
    {
        public string Branch { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Head { get; set; } = string.Empty;
        public bool IsBare { get; set; } = false;
        public bool IsDetached { get; set; } = false;
        public bool IsPrunable { get; set; } = false;

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
                    return $"(deteched HEAD at {Head.Substring(10)})";

                if (Branch.StartsWith("refs/heads/", System.StringComparison.Ordinal))
                    return $"({Branch.Substring(11)})";

                if (Branch.StartsWith("refs/remotes/", System.StringComparison.Ordinal))
                    return $"({Branch.Substring(13)})";

                return $"({Branch})";
            }
        }

        private bool _isLocked = false;
    }
}
