using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Squash : Popup
    {
        public Models.Commit Head
        {
            get;
            private set;
        }

        public Models.Commit Parent
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

        public Squash(Repository repo, Models.Commit head, Models.Commit parent)
        {
            _repo = repo;
            _message = parent.FullMessage;
            Head = head;
            Parent = parent;
            View = new Views.Squash() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Squashing ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Reset(_repo.FullPath, Parent.SHA, "--soft").Exec();
                if (succ)
                    succ = new Commands.Commit(_repo.FullPath, _message, true).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private string _message = string.Empty;
    }
}
