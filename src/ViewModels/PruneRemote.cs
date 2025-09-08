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

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Run `prune` on remote ...";

            var log = _repo.CreateLog($"Prune Remote '{Remote.Name}'");
            Use(log);

            var succ = await new Commands.Remote(_repo.FullPath)
                .Use(log)
                .PruneAsync(Remote.Name);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
