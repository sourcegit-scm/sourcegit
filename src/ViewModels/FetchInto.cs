using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class FetchInto : Popup
    {
        public Models.Branch Local
        {
            get;
            private set;
        }

        public Models.Branch Upstream
        {
            get;
            private set;
        }

        public FetchInto(Repository repo, Models.Branch local, Models.Branch upstream)
        {
            _repo = repo;
            Local = local;
            Upstream = upstream;
            View = new Views.FetchInto() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Fast-Forward ...";

            return Task.Run(() =>
            {
                new Commands.Fetch(_repo.FullPath, Local, Upstream, SetProgressDescription).Exec();
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
