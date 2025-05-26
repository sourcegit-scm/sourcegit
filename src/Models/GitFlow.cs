namespace SourceGit.Models
{
    public enum GitFlowBranchType
    {
        None = 0,
        Feature,
        Release,
        Hotfix,
    }

    public class GitFlow
    {
        public string Master { get; set; } = string.Empty;
        public string Develop { get; set; } = string.Empty;
        public string FeaturePrefix { get; set; } = string.Empty;
        public string ReleasePrefix { get; set; } = string.Empty;
        public string HotfixPrefix { get; set; } = string.Empty;

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(Master) &&
                    !string.IsNullOrEmpty(Develop) &&
                    !string.IsNullOrEmpty(FeaturePrefix) &&
                    !string.IsNullOrEmpty(ReleasePrefix) &&
                    !string.IsNullOrEmpty(HotfixPrefix);
            }
        }

        public string GetPrefix(GitFlowBranchType type)
        {
            switch (type)
            {
                case GitFlowBranchType.Feature:
                    return FeaturePrefix;
                case GitFlowBranchType.Release:
                    return ReleasePrefix;
                case GitFlowBranchType.Hotfix:
                    return HotfixPrefix;
                default:
                    return string.Empty;
            }
        }
    }
}
