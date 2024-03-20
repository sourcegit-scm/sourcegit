using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteSubmodule : Popup
    {

        public string Submodule
        {
            get;
            private set;
        }

        public DeleteSubmodule(Repository repo, string submodule)
        {
            _repo = repo;
            Submodule = submodule;
            View = new Views.DeleteSubmodule() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting submodule ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Submodule(_repo.FullPath).Delete(Submodule);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}