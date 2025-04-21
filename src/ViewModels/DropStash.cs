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

        public override Task<bool> Sure()
        {
            ProgressDescription = $"Dropping stash: {Stash.Name}";

            var log = _repo.CreateLog("Drop Stash");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.Stash(_repo.FullPath).Use(log).Drop(Stash.Name);
                log.Complete();
                return true;
            });
        }

        private readonly Repository _repo;
    }
}
