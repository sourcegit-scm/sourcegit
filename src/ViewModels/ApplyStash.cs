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

        public ApplyStash(string repo, Models.Stash stash)
        {
            _repo = repo;
            Stash = stash;
            View = new Views.ApplyStash() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            ProgressDescription = $"Applying stash: {Stash.Name}";

            return Task.Run(() =>
            {
                var succ = new Commands.Stash(_repo).Apply(Stash.Name, RestoreIndex);
                if (succ && DropAfterApply)
                    new Commands.Stash(_repo).Drop(Stash.Name);

                return true;
            });
        }

        private readonly string _repo;
    }
}
