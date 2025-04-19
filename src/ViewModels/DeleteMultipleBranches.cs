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
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting multiple branches...";

            var log = _repo.CreateLog("Delete Multiple Branches");
            Use(log);

            return Task.Run(() =>
            {
                if (_isLocal)
                {
                    foreach (var target in Targets)
                        Commands.Branch.DeleteLocal(_repo.FullPath, target.Name, log);
                }
                else
                {
                    foreach (var target in Targets)
                        Commands.Branch.DeleteRemote(_repo.FullPath, target.Remote, target.Name, log);
                }

                log.Complete();

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
