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

        public Squash(Repository repo, Models.Commit target, string shaToGetPreferMessage)
        {
            _repo = repo;
            _message = new Commands.QueryCommitFullMessage(_repo.FullPath, shaToGetPreferMessage).Result();
            Target = target;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Squashing ...";

            var log = _repo.CreateLog("Squash");
            Use(log);

            var signOff = _repo.Settings.EnableSignOffForCommit;
            var autoStashed = false;
            bool succ;

            if (_repo.LocalChangesCount > 0)
            {
                succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync("SQUASH_AUTO_STASH");
                if (!succ)
                {
                    log.Complete();
                    await CallUIThreadAsync(() => _repo.SetWatcherEnabled(true));
                    return false;
                }

                autoStashed = true;
            }

            succ = await new Commands.Reset(_repo.FullPath, Target.SHA, "--soft").Use(log).ExecAsync();
            if (succ)
                succ = await new Commands.Commit(_repo.FullPath, _message, signOff, true, false).Use(log).RunAsync();

            if (succ && autoStashed)
                await new Commands.Stash(_repo.FullPath).Use(log).PopAsync("stash@{0}");

            log.Complete();
            await CallUIThreadAsync(() => _repo.SetWatcherEnabled(true));
            return succ;
        }

        private readonly Repository _repo;
        private string _message;
    }
}
