using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteTag : Popup
    {
        public Models.Tag Target
        {
            get;
            private set;
        }

        public bool ShouldPushToRemote
        {
            get;
            set;
        }

        public DeleteTag(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            Target = tag;
            ShouldPushToRemote = true;
            View = new Views.DeleteTag() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Deleting tag '{Target.Name}' ...";

            return Task.Run(() =>
            {
                var remotes = ShouldPushToRemote ? _repo.Remotes : null;
                var succ = Commands.Tag.Delete(_repo.FullPath, Target.Name, remotes);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
