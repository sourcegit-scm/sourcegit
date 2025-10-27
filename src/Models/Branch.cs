using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum BranchSortMode
    {
        Name = 0,
        CommitterDate,
    }

    public class Branch
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public ulong CommitterDate { get; set; }
        public string Head { get; set; }
        public bool IsLocal { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsDetachedHead { get; set; }
        public string Upstream { get; set; }
        public List<string> Ahead { get; set; } = [];
        public List<string> Behind { get; set; } = [];
        public string Remote { get; set; }
        public bool IsUpstreamGone { get; set; }
        public string WorktreePath { get; set; }

        public bool HasWorktree => !IsCurrent && !string.IsNullOrEmpty(WorktreePath);
        public string FriendlyName => IsLocal ? Name : $"{Remote}/{Name}";
        public bool IsTrackStatusVisible => Ahead.Count > 0 || Behind.Count > 0;

        public string TrackStatusDescription
        {
            get
            {
                var ahead = Ahead.Count;
                var behind = Behind.Count;
                if (ahead > 0)
                    return behind > 0 ? $"{ahead}↑ {behind}↓" : $"{ahead}↑";

                return behind > 0 ? $"{behind}↓" : string.Empty;
            }
        }
    }
}
