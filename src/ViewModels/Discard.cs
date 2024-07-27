using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Discard : Popup
    {
        public object Mode
        {
            get;
            private set;
        }

        public Discard(Repository repo)
        {
            _repo = repo;

            Mode = new Models.Null();
            View = new Views.Discard { DataContext = this };
        }

        public Discard(Repository repo, List<Models.Change> changes, bool isUnstaged)
        {
            _repo = repo;
            _changes = changes;
            _isUnstaged = isUnstaged;

            if (_changes == null)
                Mode = new Models.Null();
            else if (_changes.Count == 1)
                Mode = _changes[0].Path;
            else
                Mode = _changes.Count;

            View = new Views.Discard() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = _changes == null ? "Discard all local changes ..." : $"Discard total {_changes.Count} changes ...";

            return Task.Run(() =>
            {
                if (_changes == null)
                    Commands.Discard.All(_repo.FullPath);
                else if (_isUnstaged)
                    Commands.Discard.ChangesInWorkTree(_repo.FullPath, _changes);
                else
                    Commands.Discard.ChangesInStaged(_repo.FullPath, _changes);

                CallUIThread(() =>
                {
                    _repo.MarkWorkingCopyDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });

                return true;
            });
        }

        private readonly Repository _repo = null;
        private readonly List<Models.Change> _changes = null;
        private readonly bool _isUnstaged = true;
    }
}
