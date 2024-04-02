using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CherryPick : Popup
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

        public CherryPick(Repository repo, Models.Commit target)
        {
            _repo = repo;
            Target = target;
            AutoCommit = true;
            View = new Views.CherryPick() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Cherry-Pick commit '{Target.SHA}' ...";

            return Task.Run(() =>
            {
                var succ = new Commands.CherryPick(_repo.FullPath, Target.SHA, !AutoCommit).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
