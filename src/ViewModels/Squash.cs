using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Squash : Popup
    {
        public Models.Commit Target
        {
            get => _target;
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
            _target = target;
            _message = new Commands.QueryCommitFullMessage(_repo.FullPath, shaToGetPreferMessage).Result();

            View = new Views.Squash() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Squashing ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Reset(_repo.FullPath, Target.SHA, "--soft").Exec();
                if (succ)
                    succ = new Commands.Commit(_repo.FullPath, _message, true).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo;
        private Models.Commit _target;
        private string _message;
    }
}
