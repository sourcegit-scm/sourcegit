using System;

namespace SourceGit.ViewModels
{
    public class ConflictSourceBranch
    {
        public string Name { get; private set; }
        public string Head { get; private set; }
        public Models.Commit Revision { get; private set; }

        public ConflictSourceBranch(string name, string head, Models.Commit revision)
        {
            Name = name;
            Head = head;
            Revision = revision;
        }

        public ConflictSourceBranch(Repository repo, Models.Branch branch)
        {
            Name = branch.Name;
            Head = branch.Head;
            Revision = new Commands.QuerySingleCommit(repo.FullPath, branch.Head).Result() ?? new Models.Commit() { SHA = branch.Head };
        }
    }

    public class Conflict
    {
        public string Marker
        {
            get;
            private set;
        }

        public string Description
        {
            get;
            private set;
        }
        
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
        } = false;

        public bool CanUseExternalMergeTool
        {
            get;
            private set;
        } = false;

        public Conflict(Repository repo, WorkingCopy wc, Models.Change change)
        {
            _wc = wc;
            _change = change;

            var isSubmodule = repo.Submodules.Find(x => x.Path.Equals(change.Path, StringComparison.Ordinal)) != null;
            switch (change.ConflictReason)
            {
                case Models.ConflictReason.BothDeleted:
                    Marker = "DD";
                    Description = "Both deleted";
                    break;
                case Models.ConflictReason.AddedByUs:
                    Marker = "AU";
                    Description = "Added by us";
                    break;
                case Models.ConflictReason.DeletedByThem:
                    Marker = "UD";
                    Description = "Deleted by them";
                    break;
                case Models.ConflictReason.AddedByThem:
                    Marker = "UA";
                    Description = "Added by them";
                    break;
                case Models.ConflictReason.DeletedByUs:
                    Marker = "DU";
                    Description = "Deleted by us";
                    break;
                case Models.ConflictReason.BothAdded:
                    Marker = "AA";
                    Description = "Both added";
                    if (!isSubmodule)
                    {
                        CanUseExternalMergeTool = true;
                        IsResolved = new Commands.IsConflictResolved(repo.FullPath, change).Result();
                    }
                    break;
                case Models.ConflictReason.BothModified:
                    Marker = "UU";
                    Description = "Both modified";
                    if (!isSubmodule)
                    {
                        CanUseExternalMergeTool = true;
                        IsResolved = new Commands.IsConflictResolved(repo.FullPath, change).Result();
                    }
                    break;
                default:
                    Marker = string.Empty;
                    Description = string.Empty;
                    break;
            }

            var context = wc.InProgressContext;
            if (context is CherryPickInProgress cherryPick)
            {
                Theirs = cherryPick.Head;
                Mine = new ConflictSourceBranch(repo, repo.CurrentBranch);
            }
            else if (context is RebaseInProgress rebase)
            {
                var b = repo.Branches.Find(x => x.IsLocal && x.Name == rebase.HeadName);
                if (b != null)
                    Theirs = new ConflictSourceBranch(b.Name, b.Head, rebase.StoppedAt);
                else
                    Theirs = new ConflictSourceBranch(rebase.HeadName, rebase.StoppedAt?.SHA ?? "----------", rebase.StoppedAt);

                Mine = rebase.Onto;
            }
            else if (context is RevertInProgress revert)
            {
                Theirs = revert.Head;
                Mine = new ConflictSourceBranch(repo, repo.CurrentBranch);
            }
            else if (context is MergeInProgress merge)
            {
                Theirs = merge.Source;
                Mine = new ConflictSourceBranch(repo, repo.CurrentBranch);
            }
            else
            {
                Theirs = "Stash or Patch";
                Mine = new ConflictSourceBranch(repo, repo.CurrentBranch);
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
