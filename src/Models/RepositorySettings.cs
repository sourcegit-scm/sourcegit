using System;
using System.Collections.Generic;
using System.Text;

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

        public bool EnableReflog
        {
            get;
            set;
        } = false;

        public bool EnableFirstParentInHistories
        {
            get;
            set;
        } = false;

        public bool EnableTopoOrderInHistories
        {
            get;
            set;
        } = false;

        public bool IncludeUntrackedInLocalChanges
        {
            get;
            set;
        } = true;

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

        public bool EnableForceOnFetch
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
        } = [];

        public AvaloniaList<CommitTemplate> CommitTemplates
        {
            get;
            set;
        } = [];

        public AvaloniaList<string> CommitMessages
        {
            get;
            set;
        } = [];

        public AvaloniaList<IssueTrackerRule> IssueTrackerRules
        {
            get;
            set;
        } = [];

        public AvaloniaList<CustomAction> CustomActions
        {
            get;
            set;
        } = [];

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

        public bool IsLocalBranchesExpandedInSideBar
        {
            get;
            set;
        } = true;

        public bool IsRemotesExpandedInSideBar
        {
            get;
            set;
        } = false;

        public bool IsTagsExpandedInSideBar
        {
            get;
            set;
        } = false;

        public bool IsSubmodulesExpandedInSideBar
        {
            get;
            set;
        } = false;

        public bool IsWorktreeExpandedInSideBar
        {
            get;
            set;
        } = false;

        public List<string> ExpandedBranchNodesInSideBar
        {
            get;
            set;
        } = [];

        public Dictionary<string, FilterMode> CollectHistoriesFilters()
        {
            var map = new Dictionary<string, FilterMode>();
            foreach (var filter in HistoriesFilters)
                map.Add(filter.Pattern, filter.Mode);
            return map;
        }

        public bool UpdateHistoriesFilter(string pattern, FilterType type, FilterMode mode)
        {
            // Clear all filters when there's a filter that has different mode.
            if (mode != FilterMode.None)
            {
                var clear = false;
                foreach (var filter in HistoriesFilters)
                {
                    if (filter.Mode != mode)
                    {
                        clear = true;
                        break;
                    }
                }

                if (clear)
                {
                    HistoriesFilters.Clear();
                    HistoriesFilters.Add(new Filter(pattern, type, mode));
                    return true;
                }
            }
            else
            {
                for (int i = 0; i < HistoriesFilters.Count; i++)
                {
                    var filter = HistoriesFilters[i];
                    if (filter.Type == type && filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    {
                        HistoriesFilters.RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }

            for (int i = 0; i < HistoriesFilters.Count; i++)
            {
                var filter = HistoriesFilters[i];
                if (filter.Type != type)
                    continue;

                if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    return false;
            }

            HistoriesFilters.Add(new Filter(pattern, type, mode));
            return true;
        }

        public void RemoveChildrenBranchFilters(string pattern)
        {
            var dirty = new List<Filter>();
            var prefix = $"{pattern}/";

            foreach (var filter in HistoriesFilters)
            {
                if (filter.Type == FilterType.Tag)
                    continue;

                if (filter.Pattern.StartsWith(prefix, StringComparison.Ordinal))
                    dirty.Add(filter);
            }

            foreach (var filter in dirty)
                HistoriesFilters.Remove(filter);
        }

        public string BuildHistoriesFilter()
        {
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

            bool hasIncluded = includedBranches.Count > 0 || includedRemotes.Count > 0 || includedTags.Count > 0;
            bool hasExcluded = excludedBranches.Count > 0 || excludedRemotes.Count > 0 || excludedTags.Count > 0;

            var builder = new StringBuilder();
            if (hasIncluded)
            {
                foreach (var b in includedBranches)
                {
                    builder.Append("--branches=");
                    builder.Append(b);
                    builder.Append(' ');
                }

                foreach (var r in includedRemotes)
                {
                    builder.Append("--remotes=");
                    builder.Append(r);
                    builder.Append(' ');
                }

                foreach (var t in includedTags)
                {
                    builder.Append("--tags=");
                    builder.Append(t);
                    builder.Append(' ');
                }
            }
            else if (hasExcluded)
            {
                if (excludedBranches.Count > 0)
                {
                    foreach (var b in excludedBranches)
                    {
                        builder.Append("--exclude=");
                        builder.Append(b);
                        builder.Append(" --decorate-refs-exclude=refs/heads/");
                        builder.Append(b);
                        builder.Append(' ');
                    }
                }

                builder.Append("--exclude=HEA[D] --branches ");

                if (excludedRemotes.Count > 0)
                {
                    foreach (var r in excludedRemotes)
                    {
                        builder.Append("--exclude=");
                        builder.Append(r);
                        builder.Append(" --decorate-refs-exclude=refs/remotes/");
                        builder.Append(r);
                        builder.Append(' ');
                    }
                }

                builder.Append("--exclude=origin/HEA[D] --remotes ");

                if (excludedTags.Count > 0)
                {
                    foreach (var t in excludedTags)
                    {
                        builder.Append("--exclude=");
                        builder.Append(t);
                        builder.Append(" --decorate-refs-exclude=refs/tags/");
                        builder.Append(t);
                        builder.Append(' ');
                    }
                }

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
