using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ResetWithoutCheckout : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public object To
        {
            get;
        }

        public ResetWithoutCheckout(Repository repo, Models.Branch target, Models.Branch to)
        {
            _repo = repo;
            _revision = to.Head;
            Target = target;
            To = to;
        }

        public ResetWithoutCheckout(Repository repo, Models.Branch target, Models.Commit to)
        {
            _repo = repo;
            _revision = to.SHA;
            Target = target;
            To = to;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Reset {Target.Name} to {_revision} ...";

            var log = _repo.CreateLog($"Reset '{Target.Name}' to '{_revision}'");
            Use(log);

            var succ = await Commands.Branch
                .CreateAsync(_repo.FullPath, Target.Name, _revision, true, log)
                .ConfigureAwait(false);

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return succ;
        }

        private readonly Repository _repo = null;
        private readonly string _revision;
    }
}
