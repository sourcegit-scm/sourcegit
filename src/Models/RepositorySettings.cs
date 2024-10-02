using Avalonia.Collections;

namespace SourceGit.Models
{
    public class RepositorySettings
    {
        public string DefaultRemote
        {
            get;
            set;
        } = string.Empty;
        
        public DealWithLocalChanges DealWithLocalChangesOnCheckoutBranch
        {
            get;
            set;
        } = DealWithLocalChanges.DoNothing;

        public bool FetchWithoutTags
        {
            get;
            set;
        } = false;

        public DealWithLocalChanges DealWithLocalChangesOnPull
        {
            get;
            set;
        } = DealWithLocalChanges.DoNothing;

        public bool PreferRebaseInsteadOfMerge
        {
            get;
            set;
        } = true;

        public bool FetchWithoutTagsOnPull
        {
            get;
            set;
        } = false;

        public bool FetchAllBranchesOnPull
        {
            get;
            set;
        } = true;

        public bool CheckSubmodulesOnPush
        {
            get;
            set;
        } = true;

        public bool PushAllTags
        {
            get;
            set;
        } = false;

        public DealWithLocalChanges DealWithLocalChangesOnCreateBranch
        {
            get;
            set;
        } = DealWithLocalChanges.DoNothing;

        public bool CheckoutBranchOnCreateBranch
        {
            get;
            set;
        } = true;

        public bool AutoStageBeforeCommit
        {
            get;
            set;
        } = false;

        public AvaloniaList<string> Filters
        {
            get;
            set;
        } = new AvaloniaList<string>();

        public AvaloniaList<CommitTemplate> CommitTemplates
        {
            get;
            set;
        } = new AvaloniaList<CommitTemplate>();

        public AvaloniaList<string> CommitMessages
        {
            get;
            set;
        } = new AvaloniaList<string>();

        public AvaloniaList<IssueTrackerRule> IssueTrackerRules
        {
            get;
            set;
        } = new AvaloniaList<IssueTrackerRule>();

        public bool EnableAutoFetch
        {
            get;
            set;
        } = false;

        public int AutoFetchInterval
        {
            get;
            set;
        } = 10;

        public void PushCommitMessage(string message)
        {
            var existIdx = CommitMessages.IndexOf(message);
            if (existIdx == 0)
                return;

            if (existIdx > 0)
            {
                CommitMessages.Move(existIdx, 0);
                return;
            }

            if (CommitMessages.Count > 9)
                CommitMessages.RemoveRange(9, CommitMessages.Count - 9);

            CommitMessages.Insert(0, message);
        }

        public IssueTrackerRule AddNewIssueTracker()
        {
            var rule = new IssueTrackerRule()
            {
                Name = "New Issue Tracker",
                RegexString = "#(\\d+)",
                URLTemplate = "https://xxx/$1",
            };

            IssueTrackerRules.Add(rule);
            return rule;
        }

        public IssueTrackerRule AddGithubIssueTracker(string repoURL)
        {
            var rule = new IssueTrackerRule()
            {
                Name = "Github ISSUE",
                RegexString = "#(\\d+)",
                URLTemplate = string.IsNullOrEmpty(repoURL) ? "https://github.com/username/repository/issues/$1" : $"{repoURL}/issues/$1",
            };

            IssueTrackerRules.Add(rule);
            return rule;
        }

        public IssueTrackerRule AddJiraIssueTracker()
        {
            var rule = new IssueTrackerRule()
            {
                Name = "Jira Tracker",
                RegexString = "PROJ-(\\d+)",
                URLTemplate = "https://jira.yourcompany.com/browse/PROJ-$1",
            };

            IssueTrackerRules.Add(rule);
            return rule;
        }

        public void RemoveIssueTracker(IssueTrackerRule rule)
        {
            if (rule != null)
                IssueTrackerRules.Remove(rule);
        }
    }
}
