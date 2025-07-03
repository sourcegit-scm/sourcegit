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

        public ConflictSourceBranch(Models.Branch branch, Models.Commit revision)
        {
            Name = branch.Name;
            Head = branch.Head;
            Revision = revision ?? new Models.Commit() { SHA = branch.Head };
        }
    }

    public class Conflict
    {
        public string Marker
        {
            get => _change.ConflictMarker;
        }

        public string Description
        {
            get => _change.ConflictDesc;
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
            _repo = repo;
            _wc = wc;
            _change = change;
        }

        public void Load()
        {
            var isSubmodule = _repo.Submodules.Find(x => x.Path.Equals(_change.Path, StringComparison.Ordinal)) != null;
            if (!isSubmodule && (_change.ConflictReason == Models.ConflictReason.BothAdded || _change.ConflictReason == Models.ConflictReason.BothModified))
            {
                CanUseExternalMergeTool = true;
                IsResolved = new Commands.IsConflictResolved(_repo.FullPath, _change).Result();
            }

            var context = _wc.InProgressContext;
            var revision = new Commands.QuerySingleCommit(_repo.FullPath, _repo.CurrentBranch.Head).Result();
            var mine = new ConflictSourceBranch(_repo.CurrentBranch, revision);
            if (context is CherryPickInProgress cherryPick)
            {
                Theirs = cherryPick.Head;
                Mine = mine;
            }
            else if (context is RebaseInProgress rebase)
            {
                var b = _repo.Branches.Find(x => x.IsLocal && x.Name == rebase.HeadName);
                if (b != null)
                    Theirs = new ConflictSourceBranch(b.Name, b.Head, rebase.StoppedAt);
                else
                    Theirs = new ConflictSourceBranch(rebase.HeadName, rebase.StoppedAt?.SHA ?? "----------", rebase.StoppedAt);

                Mine = rebase.Onto;
            }
            else if (context is RevertInProgress revert)
            {
                Theirs = revert.Head;
                Mine = mine;
            }
            else if (context is MergeInProgress merge)
            {
                Theirs = merge.Source;
                Mine = mine;
            }
            else
            {
                Theirs = "Stash or Patch";
                Mine = mine;
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

        private readonly Repository _repo;
        private WorkingCopy _wc = null;
        private Models.Change _change = null;
    }
}
