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

        public bool IsFetchAllRemoteVisible
        {
            get;
        }

        public bool FetchAllRemotes
        {
            get => _fetchAllRemotes;
            set
            {
                if (SetProperty(ref _fetchAllRemotes, value) && IsFetchAllRemoteVisible)
                    _repo.Settings.FetchAllRemotes = value;
            }
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
            IsFetchAllRemoteVisible = repo.Remotes.Count > 1 && preferredRemote == null;
            _fetchAllRemotes = IsFetchAllRemoteVisible && _repo.Settings.FetchAllRemotes;

            if (preferredRemote != null)
            {
                SelectedRemote = preferredRemote;
            }
            else if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
            {
                var def = _repo.Remotes.Find(r => r.Name == _repo.Settings.DefaultRemote);
                SelectedRemote = def ?? _repo.Remotes[0];
            }
            else
            {
                SelectedRemote = _repo.Remotes[0];
            }
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var navigateToUpstreamHEAD = _repo.SelectedView is Histories { AutoSelectedCommit: { IsCurrentHead: true } };
            var notags = _repo.Settings.FetchWithoutTags;
            var force = _repo.Settings.EnableForceOnFetch;
            var log = _repo.CreateLog("Fetch");
            Use(log);

            if (FetchAllRemotes)
            {
                foreach (var remote in _repo.Remotes)
                    await new Commands.Fetch(_repo.FullPath, remote.Name, notags, force)
                        .Use(log)
                        .RunAsync();
            }
            else
            {
                await new Commands.Fetch(_repo.FullPath, SelectedRemote.Name, notags, force)
                    .Use(log)
                    .RunAsync();
            }

            log.Complete();

            if (navigateToUpstreamHEAD)
            {
                var upstream = _repo.CurrentBranch?.Upstream;
                if (!string.IsNullOrEmpty(upstream))
                {
                    var upstreamHead = await new Commands.QueryRevisionByRefName(_repo.FullPath, upstream.Substring(13)).GetResultAsync();
                    _repo.NavigateToCommit(upstreamHead, true);
                }
            }

            _repo.MarkFetched();
            return true;
        }

        private readonly Repository _repo = null;
        private bool _fetchAllRemotes = false;
    }
}
