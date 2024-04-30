using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteBranch : Popup
    {
        public Models.Branch Target
        {
            get;
            private set;
        }

        public DeleteBranch(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            Target = branch;
            View = new Views.DeleteBranch() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting branch...";

            return Task.Run(() =>
            {
                if (Target.IsLocal)
                {
                    Commands.Branch.Delete(_repo.FullPath, Target.Name);
                }
                else
                {
                    new Commands.Push(_repo.FullPath, Target.Remote, Target.Name).Exec();
                }

                CallUIThread(() =>
                {
                    _repo.SetWatcherEnabled(true);
                    _repo.MarkBranchesDirtyManually();
                });
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
