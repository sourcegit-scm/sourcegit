using Avalonia.Collections;

namespace SourceGit.Models
{
    public class RepositorySettings
    {
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
        } = false;

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

        public AvaloniaList<string> CommitMessages
        {
            get;
            set;
        } = new AvaloniaList<string>();

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
    }
}
