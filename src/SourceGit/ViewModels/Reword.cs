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
            Head = head;
            Message = head.FullMessage;
            View = new Views.Reword() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            if (_message == Head.FullMessage)
                return null;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Editing head commit message ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Commit(_repo.FullPath, _message, true, true).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private string _message = string.Empty;
    }
}
