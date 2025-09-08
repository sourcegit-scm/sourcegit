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
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting multiple branches...";

            var log = _repo.CreateLog("Delete Multiple Branches");
            Use(log);

            if (_isLocal)
            {
                foreach (var target in Targets)
                    await new Commands.Branch(_repo.FullPath, target.Name)
                        .Use(log)
                        .DeleteLocalAsync();
            }
            else
            {
                foreach (var target in Targets)
                {
                    var exists = await new Commands.Remote(_repo.FullPath).HasBranchAsync(target.Remote, target.Name);
                    if (exists)
                        await new Commands.Push(_repo.FullPath, target.Remote, $"refs/heads/{target.Name}", true)
                            .Use(log)
                            .RunAsync();
                    else
                        await new Commands.Branch(_repo.FullPath, target.Name)
                            .Use(log)
                            .DeleteRemoteAsync(target.Remote);
                }
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return true;
        }

        private Repository _repo = null;
        private bool _isLocal = false;
    }
}
