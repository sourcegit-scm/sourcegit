using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DropStash : Popup
    {
        public Models.Stash Stash { get; private set; }

        public DropStash(string repo, Models.Stash stash)
        {
            _repo = repo;
            Stash = stash;
            View = new Views.DropStash() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            ProgressDescription = $"Dropping stash: {Stash.Name}";

            return Task.Run(() =>
            {
                new Commands.Stash(_repo).Drop(Stash.Name);
                return true;
            });
        }

        private readonly string _repo;
    }
}