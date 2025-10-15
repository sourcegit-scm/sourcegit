using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DropStash : Popup
    {
        public Models.Stash Stash { get; }

        public DropStash(Repository repo, Models.Stash stash)
        {
            _repo = repo;
            Stash = stash;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Dropping stash: {Stash.Name}";

            var log = _repo.CreateLog("Drop Stash");
            Use(log);

            await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .DropAsync(Stash.Name);

            log.Complete();
            _repo.MarkStashesDirtyManually();
            return true;
        }

        private readonly Repository _repo;
    }
}
