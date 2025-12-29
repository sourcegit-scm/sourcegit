using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DropHead : Popup
    {
        public Models.Commit Target
        {
            get;
        }

        public Models.Commit NewHead
        {
            get;
        }

        public DropHead(Repository repo, Models.Commit target, Models.Commit parent)
        {
            _repo = repo;
            Target = target;
            NewHead = parent;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Drop HEAD '{Target.SHA}' ...";

            var log = _repo.CreateLog($"Drop '{Target.SHA}'");
            Use(log);

            var changes = await new Commands.QueryLocalChanges(_repo.FullPath, true).GetResultAsync();
            var needAutoStash = changes.Count > 0;
            var succ = false;

            if (needAutoStash)
            {
                succ = await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PushAsync("DROP_HEAD_AUTO_STASH", false);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }
            }

            succ = await new Commands.Reset(_repo.FullPath, NewHead.SHA, "--hard")
                .Use(log)
                .ExecAsync();

            if (succ && needAutoStash)
                await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PopAsync("stash@{0}");

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
    }
}
