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
            Head = head;
        }

        public void Load()
        {
            _oldMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, Head.SHA).Result();
            _message = _oldMessage;
        }

        public override Task<bool> Sure()
        {
            if (string.Compare(_message, _oldMessage, StringComparison.Ordinal) == 0)
                return null;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Editing head commit message ...";

            var log = _repo.CreateLog("Reword HEAD");
            Use(log);

            var signOff = _repo.Settings.EnableSignOffForCommit;
            return Task.Run(() =>
            {
                // For reword (only changes the commit message), disable `--reset-author`
                var succ = new Commands.Commit(_repo.FullPath, _message, signOff, true, false).Use(log).Run();
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo;
        private string _oldMessage;
        private string _message;
    }
}
