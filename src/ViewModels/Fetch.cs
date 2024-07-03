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

        public bool Prune
        {
            get;
            set;
        } = true;

        public bool NoTags
        {
            get => _repo.Settings.FetchWithoutTags;
            set => _repo.Settings.FetchWithoutTags = value;
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

            return Task.Run(() =>
            {
                if (FetchAllRemotes)
                {
                    foreach (var remote in _repo.Remotes)
                    {
                        SetProgressDescription($"Fetching remote: {remote.Name}");
                        new Commands.Fetch(_repo.FullPath, remote.Name, Prune, NoTags, SetProgressDescription).Exec();
                    }
                }
                else
                {
                    SetProgressDescription($"Fetching remote: {SelectedRemote.Name}");
                    new Commands.Fetch(_repo.FullPath, SelectedRemote.Name, Prune, NoTags, SetProgressDescription).Exec();
                }

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
        private bool _fetchAllRemotes = true;
    }
}
