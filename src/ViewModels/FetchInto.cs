using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class FetchInto : Popup
    {
        public Models.Branch Local
        {
            get;
        }

        public Models.Branch Upstream
        {
            get;
        }

        public FetchInto(Repository repo, Models.Branch local, Models.Branch upstream)
        {
            _repo = repo;
            Local = local;
            Upstream = upstream;
            CanTerminate = true;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Fast-Forward ...";

            var log = _repo.CreateLog($"Fetch Into '{Local.FriendlyName}'");
            Use(log);

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            await new Commands.Fetch(_repo.FullPath, Local, Upstream)
                .WithCancellation(token)
                .Use(log)
                .RunAsync();

            log.Complete();

            if (_repo.SelectedViewIndex == 0 && !token.IsCancellationRequested)
            {
                var newHead = await new Commands.QueryRevisionByRefName(_repo.FullPath, Local.Name).GetResultAsync();
                _repo.NavigateToCommit(newHead, true);
            }

            _cancellation = null;
            return true;
        }

        public override void Terminate()
        {
            // Just fire cancel event and UI will auto wait the `Sure` complete
            var _ = _cancellation?.CancelAsync();
        }

        private readonly Repository _repo = null;
        private CancellationTokenSource _cancellation = null;
    }
}
