using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PruneRemote : Popup
    {
        public Models.Remote Remote
        {
            get;
        }

        public PruneRemote(Repository repo, Models.Remote remote)
        {
            _repo = repo;
            Remote = remote;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Run `prune` on remote ...";

            var log = _repo.CreateLog($"Prune Remote '{Remote.Name}'");
            Use(log);

            return Task.Run(() =>
            {
                var succ = new Commands.Remote(_repo.FullPath).Use(log).Prune(Remote.Name);
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
