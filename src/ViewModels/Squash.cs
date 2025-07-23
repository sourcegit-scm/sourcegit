using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Squash : Popup
    {
        public Models.Commit Target
        {
            get;
        }

        [Required(ErrorMessage = "Commit message is required!!!")]
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value, true);
        }

        public Squash(Repository repo, Models.Commit target, string message)
        {
            _repo = repo;
            _message = message;
            Target = target;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Squashing ...";

            var log = _repo.CreateLog("Squash");
            Use(log);

            var changes = await new Commands.QueryLocalChanges(_repo.FullPath, false).GetResultAsync();
            var signOff = _repo.Settings.EnableSignOffForCommit;
            var needAutoStash = false;
            var succ = false;

            foreach (var c in changes)
            {
                if (c.Index != Models.ChangeState.None)
                {
                    needAutoStash = true;
                    break;
                }
            }

            if (needAutoStash)
            {
                succ = await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PushAsync("SQUASH_AUTO_STASH");
                if (!succ)
                {
                    log.Complete();
                    _repo.SetWatcherEnabled(true);
                    return false;
                }
            }

            succ = await new Commands.Reset(_repo.FullPath, Target.SHA, "--soft")
                .Use(log)
                .ExecAsync();

            if (succ)
                succ = await new Commands.Commit(_repo.FullPath, _message, signOff, true, false)
                    .Use(log)
                    .RunAsync();

            if (succ && needAutoStash)
                await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PopAsync("stash@{0}");

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return succ;
        }

        private readonly Repository _repo;
        private string _message;
    }
}
