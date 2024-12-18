using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Fetch : Popup
    {
        public List<Models.Remote> Remotes
        {
            get => _repo.Remotes;
        }

        public bool FetchAllRemotes
        {
            get => _fetchAllRemotes;
            set => SetProperty(ref _fetchAllRemotes, value);
        }

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public bool NoTags
        {
            get => _repo.Settings.FetchWithoutTags;
            set => _repo.Settings.FetchWithoutTags = value;
        }

        public bool Force
        {
            get => _repo.Settings.EnableForceOnFetch;
            set => _repo.Settings.EnableForceOnFetch = value;
        }

        public Fetch(Repository repo, Models.Remote preferedRemote = null)
        {
            _repo = repo;
            _fetchAllRemotes = preferedRemote == null;
            SelectedRemote = preferedRemote != null ? preferedRemote : _repo.Remotes[0];
            View = new Views.Fetch() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            var notags = _repo.Settings.FetchWithoutTags;
            var prune = _repo.Settings.EnablePruneOnFetch;
            var force = _repo.Settings.EnableForceOnFetch;
            return Task.Run(() =>
            {
                if (FetchAllRemotes)
                {
                    foreach (var remote in _repo.Remotes)
                    {
                        SetProgressDescription($"Fetching remote: {remote.Name}");
                        new Commands.Fetch(_repo.FullPath, remote.Name, notags, prune, force, SetProgressDescription).Exec();
                    }
                }
                else
                {
                    SetProgressDescription($"Fetching remote: {SelectedRemote.Name}");
                    new Commands.Fetch(_repo.FullPath, SelectedRemote.Name, notags, prune, force, SetProgressDescription).Exec();
                }

                CallUIThread(() =>
                {
                    _repo.MarkFetched();
                    _repo.SetWatcherEnabled(true);
                });

                return true;
            });
        }

        private readonly Repository _repo = null;
        private bool _fetchAllRemotes;
    }
}
