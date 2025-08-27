using System.Collections.Generic;

namespace SourceGit.Models
{
    public class BranchTrackStatus
    {
        public List<string> Ahead { get; set; } = new List<string>();
        public List<string> Behind { get; set; } = new List<string>();

        public bool IsVisible => Ahead.Count > 0 || Behind.Count > 0;

        public override string ToString()
        {
            if (Ahead.Count == 0 && Behind.Count == 0)
                return string.Empty;

            var track = "";
            if (Ahead.Count > 0)
                track += $"{Ahead.Count}↑";
            if (Behind.Count > 0)
                track += $" {Behind.Count}↓";
            return track.Trim();
        }
    }

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
        public BranchTrackStatus TrackStatus { get; set; }
        public string Remote { get; set; }
        public bool IsUpstreamGone { get; set; }
        public string WorktreePath { get; set; }

        public bool HasWorktree => !IsCurrent && !string.IsNullOrEmpty(WorktreePath);
        public string FriendlyName => IsLocal ? Name : $"{Remote}/{Name}";
    }
}
