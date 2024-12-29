using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Revert : Popup
    {
        public Models.Commit Target
        {
            get;
            private set;
        }

        public bool AutoCommit
        {
            get;
            set;
        }

        public Revert(Repository repo, Models.Commit target)
        {
            _repo = repo;
            Target = target;
            AutoCommit = true;
            View = new Views.Revert() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Revert commit '{Target.SHA}' ...";

            return Task.Run(() =>
            {
                new Commands.Revert(_repo.FullPath, Target.SHA, AutoCommit).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
