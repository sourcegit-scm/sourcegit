using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class FastForwardWithoutCheckout : Popup
    {
        public Models.Branch Local
        {
            get;
        }

        public Models.Branch To
        {
            get;
        }

        public FastForwardWithoutCheckout(Repository repo, Models.Branch local, Models.Branch upstream)
        {
            _repo = repo;
            Local = local;
            To = upstream;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Fast-Forward ...";

            var log = _repo.CreateLog("Fast-Forward (No checkout)");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.UpdateRef(_repo.FullPath, Local.FullName, To.FullName).Use(log).Exec();
                log.Complete();
                CallUIThread(() =>
                {
                    _repo.NavigateToCommit(To.Head);
                    _repo.SetWatcherEnabled(true);
                });
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
