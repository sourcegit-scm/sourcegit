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

        public Fetch(Repository repo, Models.Remote preferredRemote = null)
        {
            _repo = repo;
            _fetchAllRemotes = preferredRemote == null;

            if (preferredRemote != null)
            {
                SelectedRemote = preferredRemote;
            }
            else if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
            {
                var def = _repo.Remotes.Find(r => r.Name == _repo.Settings.DefaultRemote);
                if (def != null)
                    SelectedRemote = def;
                else
                    SelectedRemote = _repo.Remotes[0];
            }
            else
            {
                SelectedRemote = _repo.Remotes[0];
            }
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            var notags = _repo.Settings.FetchWithoutTags;
            var force = _repo.Settings.EnableForceOnFetch;
            var log = _repo.CreateLog("Fetch");
            Use(log);

            return Task.Run(() =>
            {
                if (FetchAllRemotes)
                {
                    foreach (var remote in _repo.Remotes)
                        new Commands.Fetch(_repo.FullPath, remote.Name, notags, force).Use(log).Exec();
                }
                else
                {
                    new Commands.Fetch(_repo.FullPath, SelectedRemote.Name, notags, force).Use(log).Exec();
                }

                log.Complete();

                CallUIThread(() =>
                {
                    _repo.NavigateToBranchDelayed(_repo.CurrentBranch?.Upstream);
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
