using System;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteBranch : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public bool Force
        {
            get;
            set;
        }

        public DeleteBranch(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            Target = branch;
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
                succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                    .Use(log)
                    .DeleteLocalAsync(Force);

                if (succ)
                {
                    _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.LocalBranch);

                    var upstream = Target.Upstream ?? string.Empty;
                    var tracking = _repo.Branches.Find(x => x.FullName.Equals(upstream, StringComparison.Ordinal));
                    if (tracking != null && tracking.Name.Equals(Target.Name, StringComparison.Ordinal))
                    {
                        var msgBuilder = new StringBuilder();
                        msgBuilder
                            .AppendLine(App.Text("DeleteBranch.AskForRemote"))
                            .AppendLine()
                            .Append("• ").Append(tracking.FriendlyName);

                        var deleteTracking = await App.AskConfirmAsync(msgBuilder.ToString(), Models.ConfirmButtonType.YesNo);
                        if (deleteTracking)
                        {
                            succ = await DeleteRemoteBranchAsync(tracking, log);
                            if (succ)
                                _repo.UIStates.RemoveHistoryFilter(tracking.FullName, Models.FilterType.RemoteBranch);
                        }
                    }
                }
            }
            else
            {
                succ = await DeleteRemoteBranchAsync(Target, log);
                _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.RemoteBranch);
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return succ;
        }

        private async Task<bool> DeleteRemoteBranchAsync(Models.Branch branch, CommandLog log)
        {
            var exists = await new Commands.Remote(_repo.FullPath)
                .HasBranchAsync(branch.Remote, branch.Name)
                .ConfigureAwait(false);

            if (exists)
                return await new Commands.Push(_repo.FullPath, branch.Remote, $"refs/heads/{branch.Name}", true)
                    .Use(log)
                    .RunAsync()
                    .ConfigureAwait(false);
            else
                return await new Commands.Branch(_repo.FullPath, branch.Name)
                    .Use(log)
                    .DeleteRemoteAsync(branch.Remote)
                    .ConfigureAwait(false);
        }

        private readonly Repository _repo = null;
    }
}
