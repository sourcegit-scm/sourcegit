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
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Fast-Forward ...";

            var log = _repo.CreateLog($"Fetch Into '{Local.FriendlyName}'");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.Fetch(_repo.FullPath, Local, Upstream).Use(log).Exec();
                log.Complete();
                CallUIThread(() =>
                {
                    _repo.NavigateToBranchDelayed(Upstream.FullName);
                    _repo.SetWatcherEnabled(true);
                });
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
