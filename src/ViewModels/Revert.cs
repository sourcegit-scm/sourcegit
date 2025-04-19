using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Revert : Popup
    {
        public Models.Commit Target
        {
            get;
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
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Revert commit '{Target.SHA}' ...";

            var log = _repo.CreateLog($"Revert '{Target.SHA}'");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.Revert(_repo.FullPath, Target.SHA, AutoCommit).Use(log).Exec();
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
