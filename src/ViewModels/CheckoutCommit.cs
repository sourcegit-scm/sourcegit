using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CheckoutCommit : Popup
    {
        public Models.Commit Commit
        {
            get;
        }

        public bool DiscardLocalChanges
        {
            get;
            set;
        }

        public bool IsRecurseSubmoduleVisible
        {
            get => _repo.Submodules.Count > 0;
        }

        public bool RecurseSubmodules
        {
            get => _repo.Settings.UpdateSubmodulesOnCheckoutBranch;
            set => _repo.Settings.UpdateSubmodulesOnCheckoutBranch = value;
        }

        public CheckoutCommit(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            Commit = commit;
            DiscardLocalChanges = false;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout Commit '{Commit.SHA}' ...";

            var log = _repo.CreateLog("Checkout Commit");
            Use(log);

            var updateSubmodules = IsRecurseSubmoduleVisible && RecurseSubmodules;
            bool succ;
            var needPop = false;

            var confirmed = await _repo.ConfirmCheckoutBranchAsync();
            if (!confirmed)
            {
                _repo.SetWatcherEnabled(true);
                return true;
            }

            if (DiscardLocalChanges)
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .CommitAsync(Commit.SHA, true)
                    .ConfigureAwait(false);
            }
            else
            {
                var changes = await new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AUTO_STASH")
                        .ConfigureAwait(false);

                    if (!succ)
                    {
                        log.Complete();
                        _repo.SetWatcherEnabled(true);
                        return false;
                    }

                    needPop = true;
                }

                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .CommitAsync(Commit.SHA, false)
                    .ConfigureAwait(false);
            }

            if (succ)
            {
                if (updateSubmodules)
                {
                    var submodules = await new Commands.QueryUpdatableSubmodules(_repo.FullPath)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    if (submodules.Count > 0)
                        await new Commands.Submodule(_repo.FullPath)
                            .Use(log)
                            .UpdateAsync(submodules, true, true)
                            .ConfigureAwait(false);
                }

                if (needPop)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}")
                        .ConfigureAwait(false);
            }

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
