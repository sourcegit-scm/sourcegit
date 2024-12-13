namespace SourceGit.ViewModels
{
    public class Conflict
    {
        public object Theirs
        {
            get;
            private set;
        }

        public object Mine
        {
            get;
            private set;
        }

        public bool IsResolved
        {
            get;
            private set;
        }

        public Conflict(Repository repo, WorkingCopy wc, Models.Change change)
        {
            _wc = wc;
            _change = change;

            IsResolved = new Commands.IsConflictResolved(repo.FullPath, change).ReadToEnd().IsSuccess;

            var context = wc.InProgressContext;
            if (context is CherryPickInProgress cherryPick)
            {
                Theirs = cherryPick.Head;
                Mine = repo.CurrentBranch;
            }
            else if (context is RebaseInProgress rebase)
            {
                Theirs = repo.Branches.Find(x => x.IsLocal && x.Name == rebase.HeadName) ??
                    new Models.Branch()
                    {
                        IsLocal = true,
                        Name = rebase.HeadName,
                        FullName = $"refs/heads/{rebase.HeadName}"
                    };

                Mine = rebase.Onto;
            }
            else if (context is RevertInProgress revert)
            {
                Theirs = revert.Head;
                Mine = repo.CurrentBranch;
            }
            else if (context is MergeInProgress merge)
            {
                Theirs = merge.Source;
                Mine = repo.CurrentBranch;
            }
        }

        public void UseTheirs()
        {
            _wc.UseTheirs([_change]);
        }

        public void UseMine()
        {
            _wc.UseMine([_change]);
        }

        public void OpenExternalMergeTool()
        {
            _wc.UseExternalMergeTool(_change);
        }

        private WorkingCopy _wc = null;
        private Models.Change _change = null;
    }
}
