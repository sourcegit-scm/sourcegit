using System;
using System.Collections.Generic;
using System.Text;

using Avalonia.Collections;
using Avalonia.Threading;

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

        public bool EnablePruneOnFetch
        {
            get;
            set;
        } = false;

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

        public AvaloniaList<Filter> HistoriesFilters
        {
            get;
            set;
        } = new AvaloniaList<Filter>();

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

        public AvaloniaList<CustomAction> CustomActions
        {
            get;
            set;
        } = new AvaloniaList<CustomAction>();

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

        public bool EnableSignOffForCommit
        {
            get;
            set;
        } = false;

        public bool IncludeUntrackedWhenStash
        {
            get;
            set;
        } = true;

        public bool OnlyStagedWhenStash
        {
            get;
            set;
        } = false;

        public bool KeepIndexWhenStash
        {
            get;
            set;
        } = false;

        public string PreferedOpenAIService
        {
            get;
            set;
        } = "---";

        public FilterMode GetHistoriesFilterMode(string pattern, FilterType type)
        {
            foreach (var filter in HistoriesFilters)
            {
                if (filter.Type != type)
                    continue;

                if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    return filter.Mode;
            }

            return FilterMode.None;
        }

        public bool UpdateHistoriesFilter(string pattern, FilterType type, FilterMode mode)
        {
            for (int i = 0; i < HistoriesFilters.Count; i++)
            {
                var filter = HistoriesFilters[i];
                if (filter.Type != type)
                    continue;

                if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                {
                    if (mode == FilterMode.None)
                    {
                        HistoriesFilters.RemoveAt(i);
                        return true;
                    }

                    if (mode != filter.Mode)
                    {
                        filter.Mode = mode;
                        return true;
                    }
                }
            }

            if (mode != FilterMode.None)
            {
                HistoriesFilters.Add(new Filter(pattern, type, mode));
                return true;
            }

            return false;
        }

        public string BuildHistoriesFilter()
        {
            var builder = new StringBuilder();

            var excludedBranches = new List<string>();
            var excludedRemotes = new List<string>();
            var excludedTags = new List<string>();
            var includedBranches = new List<string>();
            var includedRemotes = new List<string>();
            var includedTags = new List<string>();
            foreach (var filter in HistoriesFilters)
            {
                if (filter.Type == FilterType.LocalBranch)
                {
                    var name = filter.Pattern.Substring(11);
                    var b = $"{name.Substring(0, name.Length - 1)}[{name[^1]}]";
                    
                    if (filter.Mode == FilterMode.Included)
                        includedBranches.Add(b);
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedBranches.Add(b);
                }
                else if (filter.Type == FilterType.LocalBranchFolder)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedBranches.Add($"{filter.Pattern.Substring(11)}/*");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedBranches.Add($"{filter.Pattern.Substring(11)}/*");
                }
                else if (filter.Type == FilterType.RemoteBranch)
                {
                    var name = filter.Pattern.Substring(13);
                    var r = $"{name.Substring(0, name.Length - 1)}[{name[^1]}]";

                    if (filter.Mode == FilterMode.Included)
                        includedRemotes.Add(r);
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedRemotes.Add(r);
                }
                else if (filter.Type == FilterType.RemoteBranchFolder)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRemotes.Add($"{filter.Pattern.Substring(13)}/*");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedRemotes.Add($"{filter.Pattern.Substring(13)}/*");
                }
                else if (filter.Type == FilterType.Tag)
                {
                    var name = filter.Pattern;
                    var t = $"{name.Substring(0, name.Length - 1)}[{name[^1]}]";

                    if (filter.Mode == FilterMode.Included)
                        includedTags.Add(t);
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedTags.Add(t);
                }
            }

            foreach (var b in excludedBranches)
            {
                builder.Append("--exclude=");
                builder.Append(b);
                builder.Append(' ');
            }

            if (includedBranches.Count > 0)
            {
                foreach (var b in includedBranches)
                {
                    builder.Append("--branches=");
                    builder.Append(b);
                    builder.Append(' ');
                }
            }
            else if (excludedBranches.Count > 0)
            {
                builder.Append("--branches ");
            }

            foreach (var r in excludedRemotes)
            {
                builder.Append("--exclude=");
                builder.Append(r);
                builder.Append(' ');
            }

            if (includedRemotes.Count > 0)
            {
                foreach (var r in includedRemotes)
                {
                    builder.Append("--remotes=");
                    builder.Append(r);
                    builder.Append(' ');
                }
            }
            else if (excludedRemotes.Count > 0)
            {
                builder.Append("--remotes ");
            }

            foreach (var t in excludedTags)
            {
                builder.Append("--exclude=");
                builder.Append(t);
                builder.Append(' ');
            }

            if (includedTags.Count > 0)
            {
                foreach (var t in includedTags)
                {
                    builder.Append("--tags=");
                    builder.Append(t);
                    builder.Append(' ');
                }
            }
            else if (excludedTags.Count > 0)
            {
                builder.Append("--tags ");
            }

            return builder.ToString();
        }

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

        public IssueTrackerRule AddGitLabIssueTracker(string repoURL)
        {
            var rule = new IssueTrackerRule()
            {
                Name = "GitLab ISSUE",
                RegexString = "#(\\d+)",
                URLTemplate = string.IsNullOrEmpty(repoURL) ? "https://gitlab.com/username/repository/-/issues/$1" : $"{repoURL}/-/issues/$1",
            };

            IssueTrackerRules.Add(rule);
            return rule;
        }

        public IssueTrackerRule AddGitLabMergeRequestTracker(string repoURL)
        {
            var rule = new IssueTrackerRule()
            {
                Name = "GitLab MR",
                RegexString = "!(\\d+)",
                URLTemplate = string.IsNullOrEmpty(repoURL) ? "https://gitlab.com/username/repository/-/merge_requests/$1" : $"{repoURL}/-/merge_requests/$1",
            };

            IssueTrackerRules.Add(rule);
            return rule;
        }

        public void RemoveIssueTracker(IssueTrackerRule rule)
        {
            if (rule != null)
                IssueTrackerRules.Remove(rule);
        }

        public CustomAction AddNewCustomAction()
        {
            var act = new CustomAction()
            {
                Name = "Unnamed Custom Action",
            };

            CustomActions.Add(act);
            return act;
        }

        public void RemoveCustomAction(CustomAction act)
        {
            if (act != null)
                CustomActions.Remove(act);
        }
    }
}
