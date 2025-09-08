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
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Reset {Target.Name} to {_revision} ...";

            var log = _repo.CreateLog($"Reset '{Target.Name}' to '{_revision}'");
            Use(log);

            var succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                .Use(log)
                .CreateAsync(_revision, true);

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return succ;
        }

        private readonly Repository _repo = null;
        private readonly string _revision;
    }
}
