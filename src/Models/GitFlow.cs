namespace SourceGit.Models {
    /// <summary>
    ///     GitFlow的分支类型
    /// </summary>
    public enum GitFlowBranchType {
        None,
        Feature,
        Release,
        Hotfix,
    }

    /// <summary>
    ///     GitFlow相关设置
    /// </summary>
    public class GitFlow {
        public string Feature { get; set; }
        public string Release { get; set; }
        public string Hotfix { get; set; }

        public bool IsEnabled {
            get {
                return !string.IsNullOrEmpty(Feature)
                    && !string.IsNullOrEmpty(Release)
                    && !string.IsNullOrEmpty(Hotfix);
            }
        }

        public GitFlowBranchType GetBranchType(string name) {
            if (!IsEnabled) return GitFlowBranchType.None;
            if (name.StartsWith(Feature)) return GitFlowBranchType.Feature;
            if (name.StartsWith(Release)) return GitFlowBranchType.Release;
            if (name.StartsWith(Hotfix)) return GitFlowBranchType.Hotfix;
            return GitFlowBranchType.None;
        }
    }
}
