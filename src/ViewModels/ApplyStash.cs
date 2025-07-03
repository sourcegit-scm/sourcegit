using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ApplyStash : Popup
    {
        public Models.Stash Stash
        {
            get;
            private set;
        }

        public bool RestoreIndex
        {
            get;
            set;
        } = true;

        public bool DropAfterApply
        {
            get;
            set;
        } = false;

        public ApplyStash(Repository repo, Models.Stash stash)
        {
            _repo = repo;
            Stash = stash;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Applying stash: {Stash.Name}";

            var log = _repo.CreateLog("Apply Stash");
            Use(log);

            var succ = await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .ApplyAsync(Stash.Name, RestoreIndex)
                .ConfigureAwait(false);

            if (succ && DropAfterApply)
                await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .DropAsync(Stash.Name)
                    .ConfigureAwait(false);

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return true;
        }

        private readonly Repository _repo;
    }
}
