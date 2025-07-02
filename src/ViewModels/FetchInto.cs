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

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Fast-Forward ...";

            var log = _repo.CreateLog($"Fetch Into '{Local.FriendlyName}'");
            Use(log);

            {
                await new Commands.Fetch(_repo.FullPath, Local, Upstream).Use(log).ExecAsync();
                log.Complete();

                var changedLocalBranchHead = await new Commands.QueryRevisionByRefName(_repo.FullPath, Local.Name).ResultAsync();
                await CallUIThreadAsync(() =>
                {
                    _repo.NavigateToCommit(changedLocalBranchHead, true);
                    _repo.SetWatcherEnabled(true);
                });

                return true;
            }
        }

        private readonly Repository _repo = null;
    }
}
