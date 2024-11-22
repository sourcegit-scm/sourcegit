using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteMultipleBranches : Popup
    {
        public List<Models.Branch> Targets
        {
            get;
        }

        public DeleteMultipleBranches(Repository repo, List<Models.Branch> branches, bool isLocal)
        {
            _repo = repo;
            _isLocal = isLocal;
            Targets = branches;
            View = new Views.DeleteMultipleBranches() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting multiple branches...";

            return Task.Run(() =>
            {
                if (_isLocal)
                {
                    foreach (var target in Targets)
                    {
                        SetProgressDescription($"Deleting local branch : {target.Name}");
                        Commands.Branch.DeleteLocal(_repo.FullPath, target.Name);
                    }
                }
                else
                {
                    foreach (var target in Targets)
                    {
                        SetProgressDescription($"Deleting remote branch : {target.FriendlyName}");
                        Commands.Branch.DeleteRemote(_repo.FullPath, target.Remote, target.Name);
                    }
                }

                CallUIThread(() =>
                {
                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });

                return true;
            });
        }

        private Repository _repo = null;
        private bool _isLocal = false;
    }
}
