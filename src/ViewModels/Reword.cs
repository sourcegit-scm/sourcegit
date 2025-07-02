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

        public Reword(Repository repo, Models.Commit head)
        {
            _repo = repo;
            _oldMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, head.SHA).Result();
            _message = _oldMessage;
            Head = head;
        }

        public override async Task<bool> Sure()
        {
            if (string.Compare(_message, _oldMessage, StringComparison.Ordinal) == 0)
                return true;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Editing head commit message ...";

            var log = _repo.CreateLog("Reword HEAD");
            Use(log);

            var signOff = _repo.Settings.EnableSignOffForCommit;
            {
                // For reword (only changes the commit message), disable `--reset-author`
                var succ = await new Commands.Commit(_repo.FullPath, _message, signOff, true, false).Use(log).RunAsync();
                log.Complete();
                await CallUIThreadAsync(() => _repo.SetWatcherEnabled(true));
                return succ;
            }
        }

        private readonly Repository _repo;
        private readonly string _oldMessage;
        private string _message;
    }
}
