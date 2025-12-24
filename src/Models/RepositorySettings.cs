using System.Collections.Generic;
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

        public bool NoVerifyOnCommit
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

        public string ConventionalTypesOverride
        {
            get;
            set;
        } = string.Empty;

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
