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
            private set;
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
            View = new Views.Reword() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            if (string.Compare(_message, _oldMessage, StringComparison.Ordinal) == 0)
                return null;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Editing head commit message ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Commit(_repo.FullPath, _message, false, true, true).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private string _message = string.Empty;
        private string _oldMessage = string.Empty;
    }
}
