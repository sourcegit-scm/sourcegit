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

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            _repo.ClearCommitMessage();
            ProgressDescription = $"Revert commit '{Target.SHA}' ...";

            var log = _repo.CreateLog($"Revert '{Target.SHA}'");
            Use(log);

            {
                await new Commands.Revert(_repo.FullPath, Target.SHA, AutoCommit).Use(log).ExecAsync();
                log.Complete();
                await CallUIThreadAsync(() => _repo.SetWatcherEnabled(true));
                return true;
            }
        }

        private readonly Repository _repo = null;
    }
}
