namespace SourceGit.Models
{
    public static class HgTerminology
    {
        public static string Extend(string key, string label, bool enabled)
        {
            if (!enabled)
                return label;

            var equivalent = key switch
            {
                "BranchCM.Checkout" or "CommitCM.Checkout" or "TagCM.Checkout" => "update",
                "CreateBranch" or "Repository.NewBranch" => "bookmark",
                "BranchCM.Rename" => "rename bookmark",
                "BranchCM.Delete" or "BranchCM.DeleteMultiBranches" => "delete bookmark",
                "BranchCM.ResetToSelectedCommit" => "move bookmark",
                "BranchCM.FastForward" => "update bookmark",
                "Fetch" or "RemoteCM.Fetch" or "BranchCM.FetchInto" or "RemoteCM.EnableAutoFetch" => "pull",
                "CommitCM.CherryPick" or "CommitCM.CherryPickMultiple" => "graft",
                "CommitCM.Revert" => "backout",
                "CommitCM.InteractiveRebase" or "CommitCM.InteractiveRebase.Manually" or "BranchCM.InteractiveRebase.Manually" => "histedit",
                "CommitCM.InteractiveRebase.Reword" => "histedit mess",
                "CommitCM.InteractiveRebase.Squash" => "histedit fold",
                "CommitCM.InteractiveRebase.Fixup" => "histedit roll",
                "ChangeCM.CheckoutThisRevision" or "ChangeCM.CheckoutFirstParentRevision" or "ChangeCM.ResetFileTo" or "FileCM.Discard" or "FileCM.DiscardMulti" => "revert",
                "Stash" or "FileCM.Stash" or "FileCM.StashMulti" => "shelve",
                "StashCM.Apply" => "unshelve --keep",
                "StashCM.Drop" => "shelve --delete",
                "Blame" or "Blame.BlameOnPreviousRevision" => "annotate",
                "Stashes" => "shelves",
                "Repository.ClearStashes" => "shelve --cleanup",
                "Stash.IncludeUntracked" => "shelve --unknown",
                "CreateBranch.OverwriteExisting" => "bookmark --force",
                "Apply.Title" => "import --no-commit",
                "WorkingCopy.AddToGitIgnore" => ".hgignore",
                _ => null,
            };

            return equivalent == null ? label : $"{label} {{ Hg: {equivalent} }}";
        }
    }
}
