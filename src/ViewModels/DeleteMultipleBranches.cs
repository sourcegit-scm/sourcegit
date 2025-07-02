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

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting multiple branches...";

            var log = _repo.CreateLog("Delete Multiple Branches");
            Use(log);

            if (_isLocal)
            {
                foreach (var target in Targets)
                    await Commands.Branch.DeleteLocalAsync(_repo.FullPath, target.Name, log);
            }
            else
            {
                foreach (var target in Targets)
                    await Commands.Branch.DeleteRemoteAsync(_repo.FullPath, target.Remote, target.Name, log);
            }

            log.Complete();

            await CallUIThreadAsync(() =>
            {
                _repo.MarkBranchesDirtyManually();
                _repo.SetWatcherEnabled(true);
            });

            return true;
        }

        private Repository _repo = null;
        private bool _isLocal = false;
    }
}
