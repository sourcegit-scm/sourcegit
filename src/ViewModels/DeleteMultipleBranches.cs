using System;
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

        public bool Force
        {
            get;
            set;
        } = false;

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
                        .DeleteLocalAsync(Force);
            }
            else
            {
                foreach (var target in Targets)
                    await DeleteRemoteBranchAsync(target, log);
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return true;
        }

        private async Task<bool> DeleteRemoteBranchAsync(Models.Branch branch, CommandLog log)
        {
            var exists = false;
            var remote = _repo.Remotes.Find(x => x.Name.Equals(branch.Remote, StringComparison.Ordinal));
            if (remote != null)
            {
                exists = await new Commands.DoesBranchExistOnRemote(_repo.FullPath, remote, branch)
                    .Use(log)
                    .GetResultAsync()
                    .ConfigureAwait(false);
            }

            if (exists)
                return await new Commands.Push(_repo.FullPath, remote, $"refs/heads/{branch.Name}", true)
                    .Use(log)
                    .ExecAsync()
                    .ConfigureAwait(false);
            else
                return await new Commands.Branch(_repo.FullPath, branch.Name)
                    .Use(log)
                    .DeleteRemoteAsync(branch.Remote, Force)
                    .ConfigureAwait(false);
        }

        private Repository _repo = null;
        private bool _isLocal = false;
    }
}
