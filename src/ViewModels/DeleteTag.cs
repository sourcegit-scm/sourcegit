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

        public bool PushToRemotes
        {
            get => _repo.Settings.PushToRemoteWhenDeleteTag;
            set => _repo.Settings.PushToRemoteWhenDeleteTag = value;
        }

        public DeleteTag(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            Target = tag;
            View = new Views.DeleteTag() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Deleting tag '{Target.Name}' ...";

            var remotes = PushToRemotes ? _repo.Remotes : null;
            return Task.Run(() =>
            {
                var succ = Commands.Tag.Delete(_repo.FullPath, Target.Name, remotes);
                CallUIThread(() =>
                {
                    _repo.MarkTagsDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
