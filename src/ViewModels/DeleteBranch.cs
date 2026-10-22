using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteBranch : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public Models.Branch Upstream
        {
            get;
        }

        public string DeleteUpstreamTip
        {
            get;
        }

        public bool DeleteUpstream
        {
            get;
            set;
        }

        public bool Force
        {
            get;
            set;
        }

        public List<Worktree> AttachedWorktrees
        {
            get;
        } = [];

        public bool HasAttachedWorktrees
        {
            get => AttachedWorktrees.Count > 0;
        }

        public bool DeleteWorktrees
        {
            get;
            set;
        }

        public DeleteBranch(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            Target = branch;

            if (branch.IsLocal && !string.IsNullOrEmpty(branch.Upstream))
            {
                var upstream = _repo.Branches.Find(x => x.FullName.Equals(branch.Upstream, StringComparison.Ordinal));
                if (upstream != null && upstream.Name.Equals(branch.Name, StringComparison.Ordinal))
                {
                    Upstream = upstream;
                    DeleteUpstreamTip = App.Text("DeleteBranch.WithTrackingRemote", upstream.FriendlyName);
                }
            }

            if (branch.IsLocal)
            {
                foreach (var wt in _repo.Worktrees)
                {
                    if (!wt.IsMain && !wt.IsCurrent && wt.Backend.Branch.Equals(branch.FullName, StringComparison.Ordinal))
                        AttachedWorktrees.Add(wt);
                }
            }
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting branch...";

            var log = _repo.CreateLog("Delete Branch");
            Use(log);

            var succ = false;
            if (Target.IsLocal)
            {
                do
                {
                    if (DeleteWorktrees)
                    {
                        // Re-read worktrees, since a previous (failed) attempt may have already removed some of them.
                        succ = true;
                        var current = await new Commands.Worktree(_repo.FullPath).ReadAllAsync();
                        foreach (var wt in AttachedWorktrees)
                        {
                            if (!current.Exists(x => x.FullPath.Equals(wt.FullPath, StringComparison.Ordinal)))
                                continue;

                            succ = await new Commands.Worktree(_repo.FullPath)
                                .Use(log)
                                .RemoveAsync(wt.FullPath, Force);

                            if (!succ)
                                break;
                        }

                        if (!succ)
                            break;
                    }

                    succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                        .Use(log)
                        .DeleteLocalAsync(Force);

                    if (!succ)
                        break;

                    _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.LocalBranch);

                    if (!DeleteUpstream || Upstream == null)
                        break;

                    succ = await DeleteRemoteBranchAsync(Upstream, log);
                    if (!succ)
                        break;

                    _repo.UIStates.RemoveHistoryFilter(Upstream.FullName, Models.FilterType.RemoteBranch);
                } while (false);
            }
            else
            {
                succ = await DeleteRemoteBranchAsync(Target, log);
                if (succ)
                    _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.RemoteBranch);
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return succ;
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

        private readonly Repository _repo = null;
    }
}
