using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
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

        public bool CanMerge
        {
            get;
            private set;
        } = false;

        public Conflict(Repository repo, WorkingCopy wc, Models.Change change)
        {
            _repo = repo;
            _wc = wc;
            _change = change;

            CanMerge = _change.ConflictReason is Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified;
            if (CanMerge)
                CanMerge = !Directory.Exists(Path.Combine(repo.FullPath, change.Path)); // Cannot merge directories (submodules)

            if (CanMerge)
                IsResolved = new Commands.IsConflictResolved(repo.FullPath, change).GetResult();

            var head = new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResult();
            (Mine, Theirs) = wc.InProgressContext switch
            {
                CherryPickInProgress cherryPick => (head, cherryPick.Head),
                RebaseInProgress rebase => (rebase.Onto, rebase.StoppedAt),
                RevertInProgress revert => (head, revert.Head),
                MergeInProgress merge => (head, merge.Source),
                _ => (head, (object)"Stash or Patch"),
            };
        }

        public async Task UseTheirsAsync()
        {
            await _wc.UseTheirsAsync([_change]);
        }

        public async Task UseMineAsync()
        {
            await _wc.UseMineAsync([_change]);
        }

        public async Task MergeAsync()
        {
            if (CanMerge)
                await App.ShowDialog(new MergeConflictEditor(_repo, _change.Path));
        }

        public async Task MergeExternalAsync()
        {
            if (CanMerge)
                await _wc.UseExternalMergeToolAsync(_change);
        }

        private Repository _repo = null;
        private WorkingCopy _wc = null;
        private Models.Change _change = null;
    }
}
