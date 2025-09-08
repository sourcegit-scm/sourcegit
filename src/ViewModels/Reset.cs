using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Reset : Popup
    {
        public Models.Branch Current
        {
            get;
        }

        public Models.Commit To
        {
            get;
        }

        public Models.ResetMode SelectedMode
        {
            get;
            set;
        }

        public Reset(Repository repo, Models.Branch current, Models.Commit to)
        {
            _repo = repo;
            Current = current;
            To = to;
            SelectedMode = Models.ResetMode.Supported[1];
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Reset current branch to {To.SHA} ...";

            var log = _repo.CreateLog($"Reset HEAD to '{To.SHA}'");
            Use(log);

            var succ = await new Commands.Reset(_repo.FullPath, To.SHA, SelectedMode.Arg)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
