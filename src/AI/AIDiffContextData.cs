using System.Collections.Generic;

namespace SourceGit.AI
{
    public enum DiffScenario { WorkingTree, CommitRange }

    public class AIDiffContextData
    {
        public DiffScenario Scenario { get; set; }

        public string DiffStatText { get; set; } = string.Empty;
        public string NameStatusText { get; set; } = string.Empty;
        public string FullDiffText { get; set; } = string.Empty;
        public bool IsTruncated { get; set; }
        public int TotalFiles { get; set; }
        public int TotalInsertions { get; set; }
        public int TotalDeletions { get; set; }
        public List<string> SkippedBinaryFiles { get; set; } = [];
        public List<string> SkippedLargeFiles { get; set; } = [];

        public string FromSHA { get; set; } = string.Empty;
        public string ToSHA { get; set; } = string.Empty;
        public string CommitLogText { get; set; } = string.Empty;

        public string StagedStatText { get; set; } = string.Empty;
        public string UnstagedStatText { get; set; } = string.Empty;
        public string StagedNameStatus { get; set; } = string.Empty;
        public string UnstagedNameStatus { get; set; } = string.Empty;
    }
}
