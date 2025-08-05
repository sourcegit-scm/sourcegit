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

        public HistoryShowFlags HistoryShowFlags
        {
            get;
            set;
        } = HistoryShowFlags.None;

        public bool EnableTopoOrderInHistories
        {
            get;
            set;
        } = false;

        public bool OnlyHighlightCurrentBranchInHistories
        {
            get;
            set;
        } = false;

        public BranchSortMode LocalBranchSortMode
        {
            get;
            set;
        } = BranchSortMode.Name;

        public BranchSortMode RemoteBranchSortMode
        {
            get;
            set;
        } = BranchSortMode.Name;

        public TagSortMode TagSortMode
        {
            get;
            set;
        } = TagSortMode.CreatorDate;

        public bool IncludeUntrackedInLocalChanges
        {
            get;
            set;
        } = true;

        public bool EnableForceOnFetch
        {
            get;
            set;
        } = false;

        public bool FetchAllRemotes
        {
            get;
            set;
        } = false;

        public bool FetchWithoutTags
        {
            get;
            set;
        } = false;

        public bool PreferRebaseInsteadOfMerge
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

        public bool PushToRemoteWhenCreateTag
        {
            get;
            set;
        } = true;

        public bool PushToRemoteWhenDeleteTag
        {
            get;
            set;
        } = false;

        public bool CheckoutBranchOnCreateBranch
        {
            get;
            set;
        } = true;

        public bool UpdateSubmodulesOnCheckoutBranch
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

        public int ChangesAfterStashing
        {
            get;
            set;
        } = 0;

        public string PreferredOpenAIService
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

        public int PreferredMergeMode
        {
            get;
            set;
        } = 0;

        public string LastCommitMessage
        {
            get;
            set;
        } = string.Empty;

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

            foreach (var filter in HistoriesFilters)
            {
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
            var includedRefs = new List<string>();
            var excludedBranches = new List<string>();
            var excludedRemotes = new List<string>();
            var excludedTags = new List<string>();
            foreach (var filter in HistoriesFilters)
            {
                if (filter.Type == FilterType.LocalBranch)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add(filter.Pattern);
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedBranches.Add($"--exclude=\"{filter.Pattern.AsSpan(11)}\" --decorate-refs-exclude=\"{filter.Pattern}\"");
                }
                else if (filter.Type == FilterType.LocalBranchFolder)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add($"--branches={filter.Pattern.AsSpan(11)}/*");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedBranches.Add($"--exclude=\"{filter.Pattern.AsSpan(11)}/*\" --decorate-refs-exclude=\"{filter.Pattern}/*\"");
                }
                else if (filter.Type == FilterType.RemoteBranch)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add(filter.Pattern);
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedRemotes.Add($"--exclude=\"{filter.Pattern.AsSpan(13)}\" --decorate-refs-exclude=\"{filter.Pattern}\"");
                }
                else if (filter.Type == FilterType.RemoteBranchFolder)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add($"--remotes={filter.Pattern.AsSpan(13)}/*");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedRemotes.Add($"--exclude=\"{filter.Pattern.AsSpan(13)}/*\" --decorate-refs-exclude=\"{filter.Pattern}/*\"");
                }
                else if (filter.Type == FilterType.Tag)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add($"refs/tags/{filter.Pattern}");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedTags.Add($"--exclude=\"{filter.Pattern}\" --decorate-refs-exclude=\"refs/tags/{filter.Pattern}\"");
                }
            }

            var builder = new StringBuilder();
            if (includedRefs.Count > 0)
            {
                foreach (var r in includedRefs)
                {
                    builder.Append(r);
                    builder.Append(' ');
                }
            }
            else if (excludedBranches.Count + excludedRemotes.Count + excludedTags.Count > 0)
            {
                foreach (var b in excludedBranches)
                {
                    builder.Append(b);
                    builder.Append(' ');
                }

                builder.Append("--exclude=HEAD --branches ");

                foreach (var r in excludedRemotes)
                {
                    builder.Append(r);
                    builder.Append(' ');
                }

                builder.Append("--exclude=origin/HEAD --remotes ");

                foreach (var t in excludedTags)
                {
                    builder.Append(t);
                    builder.Append(' ');
                }

                builder.Append("--tags ");
            }

            return builder.ToString();
        }

        public void PushCommitMessage(string message)
        {
            message = message.Trim().ReplaceLineEndings("\n");
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

        public CustomAction AddNewCustomAction()
        {
            var act = new CustomAction() { Name = "Unnamed Action" };
            CustomActions.Add(act);
            return act;
        }

        public void RemoveCustomAction(CustomAction act)
        {
            if (act != null)
                CustomActions.Remove(act);
        }

        public void MoveCustomActionUp(CustomAction act)
        {
            var idx = CustomActions.IndexOf(act);
            if (idx > 0)
                CustomActions.Move(idx - 1, idx);
        }

        public void MoveCustomActionDown(CustomAction act)
        {
            var idx = CustomActions.IndexOf(act);
            if (idx < CustomActions.Count - 1)
                CustomActions.Move(idx + 1, idx);
        }
    }
}
