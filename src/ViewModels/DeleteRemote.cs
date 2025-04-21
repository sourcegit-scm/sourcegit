using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteRemote : Popup
    {
        public Models.Remote Remote
        {
            get;
            private set;
        }

        public DeleteRemote(Repository repo, Models.Remote remote)
        {
            _repo = repo;
            Remote = remote;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting remote ...";

            var log = _repo.CreateLog("Delete Remote");
            Use(log);

            return Task.Run(() =>
            {
                var succ = new Commands.Remote(_repo.FullPath).Use(log).Delete(Remote.Name);
                log.Complete();

                CallUIThread(() =>
                {
                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
