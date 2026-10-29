using System.Collections.Generic;
using System.Threading;
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
            set => SetProperty(ref _fetchAllRemotes, value);
        }

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public bool NoTags
        {
            get => _noTags;
            set => SetProperty(ref _noTags, value);
        }

        public bool Force
        {
            get => _force;
            set => SetProperty(ref _force, value);
        }

        public override bool HasDefaultOptions
        {
            get => true;
        }

        public Fetch(Repository repo, Models.Remote preferredRemote = null)
        {
            _repo = repo;
            IsFetchAllRemoteVisible = repo.Remotes.Count > 1 && preferredRemote == null;
            _fetchAllRemotes = IsFetchAllRemoteVisible && _repo.UIStates.FetchAllRemotes;
            _noTags = _repo.UIStates.FetchWithoutTags;
            _force = _repo.UIStates.EnableForceOnFetch;
            CanTerminate = true;

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

        public override void SaveOptionsAsDefault()
        {
            if (IsFetchAllRemoteVisible)
                _repo.UIStates.FetchAllRemotes = _fetchAllRemotes;

            _repo.UIStates.FetchWithoutTags = _noTags;
            _repo.UIStates.EnableForceOnFetch = _force;
            _repo.UIStates.Save();
        }

        public override void ResetDefaultOptions()
        {
            var defaults = new Models.RepositoryUIStates();
            _repo.UIStates.FetchAllRemotes = defaults.FetchAllRemotes;
            _repo.UIStates.FetchWithoutTags = defaults.FetchWithoutTags;
            _repo.UIStates.EnableForceOnFetch = defaults.EnableForceOnFetch;
            _repo.UIStates.Save();

            FetchAllRemotes = IsFetchAllRemoteVisible && defaults.FetchAllRemotes;
            NoTags = defaults.FetchWithoutTags;
            Force = defaults.EnableForceOnFetch;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var navigateToUpstreamHEAD = _repo.IsDashboardVisible &&
                _repo.Histories.SelectedCommits.Count == 1 &&
                _repo.Histories.SelectedCommits[0].IsCurrentHead;

            var notags = _noTags;
            var force = _force;
            var log = _repo.CreateLog("Fetch");
            Use(log);

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            var succ = true;
            if (FetchAllRemotes)
            {
                foreach (var remote in _repo.Remotes)
                {
                    succ &= await new Commands.Fetch(_repo.FullPath, remote, notags, force)
                        .WithCancellation(token)
                        .Use(log)
                        .ExecAsync();

                    if (token.IsCancellationRequested)
                        break;
                }
            }
            else
            {
                succ = await new Commands.Fetch(_repo.FullPath, SelectedRemote, notags, force)
                    .WithCancellation(token)
                    .Use(log)
                    .ExecAsync();
            }

            log.Complete();

            if (navigateToUpstreamHEAD && !token.IsCancellationRequested)
            {
                var upstream = _repo.CurrentBranch?.Upstream;
                if (!string.IsNullOrEmpty(upstream))
                {
                    var upstreamHead = await new Commands.QueryRevisionByRefName(_repo.FullPath, upstream.Substring(13)).GetResultAsync();
                    _repo.NavigateToCommit(upstreamHead, true);
                }
            }

            if (!token.IsCancellationRequested)
                _repo.MarkFetched();

            _cancellation = null;
            return succ;
        }

        public override void Terminate()
        {
            // Just fire cancel event and UI will auto wait the `Sure` complete
            var _ = _cancellation?.CancelAsync();
        }

        private readonly Repository _repo = null;
        private bool _fetchAllRemotes = false;
        private bool _noTags = false;
        private bool _force = false;
        private CancellationTokenSource _cancellation = null;
    }
}
