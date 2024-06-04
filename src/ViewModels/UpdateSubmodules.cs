using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class UpdateSubmodules : Popup
    {
        public UpdateSubmodules(Repository repo)
        {
            _repo = repo;
            View = new Views.UpdateSubmodules() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Updating submodules ...";

            return Task.Run(() =>
            {
                new Commands.Submodule(_repo.FullPath).Update(SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
