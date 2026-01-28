using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Reword : Popup
    {
        public Models.Commit Head
        {
            get;
        }

        [Required(ErrorMessage = "Commit message is required!!!")]
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value, true);
        }

        public Reword(Repository repo, Models.Commit head, string oldMessage)
        {
            _repo = repo;
            _oldMessage = oldMessage;
            _message = _oldMessage;
            Head = head;
        }

        public override async Task<bool> Sure()
        {
            if (string.Compare(_message, _oldMessage, StringComparison.Ordinal) == 0)
                return true;

            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Editing HEAD message ...";

            var log = _repo.CreateLog("Reword HEAD");
            Use(log);

            var changes = await new Commands.QueryLocalChanges(_repo.FullPath, false).GetResultAsync();
            var signOff = _repo.Settings.EnableSignOffForCommit;
            var noVerify = _repo.Settings.NoVerifyOnCommit;
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
                    .PushAsync("REWORD_AUTO_STASH", false);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }
            }

            succ = await new Commands.Commit(_repo.FullPath, _message, signOff, noVerify, true, false)
                .Use(log)
                .RunAsync();

            if (succ && needAutoStash)
                await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PopAsync("stash@{0}");

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private readonly string _oldMessage;
        private string _message;
    }
}
