using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CheckoutAndFastForward : Popup
    {
        public Models.Branch LocalBranch
        {
            get;
        }

        public Models.Branch RemoteBranch
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

        public CheckoutAndFastForward(Repository repo, Models.Branch localBranch, Models.Branch remoteBranch)
        {
            _repo = repo;
            LocalBranch = localBranch;
            RemoteBranch = remoteBranch;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Checkout and Fast-Forward '{LocalBranch.Name}' ...";

            var log = _repo.CreateLog($"Checkout and Fast-Forward '{LocalBranch.Name}' ...");
            Use(log);

            if (_repo.CurrentBranch is { IsDetachedHead: true })
            {
                var refs = await new Commands.QueryRefsContainsCommit(_repo.FullPath, _repo.CurrentBranch.Head).GetResultAsync();
                if (refs.Count == 0)
                {
                    var msg = App.Text("Checkout.WarnLostCommits");
                    var shouldContinue = await App.AskConfirmAsync(msg);
                    if (!shouldContinue)
                        return true;
                }
            }

            var succ = false;
            var needPopStash = false;

            if (!DiscardLocalChanges)
            {
                var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AND_FASTFORWARD_AUTO_STASH");
                    if (!succ)
                    {
                        log.Complete();
                        return false;
                    }

                    needPopStash = true;
                }
            }

            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .BranchAsync(LocalBranch.Name, RemoteBranch.Head, DiscardLocalChanges, true);

            if (succ)
            {
                if (IsRecurseSubmoduleVisible && RecurseSubmodules)
                {
                    var submodules = await new Commands.QueryUpdatableSubmodules(_repo.FullPath).GetResultAsync();
                    if (submodules.Count > 0)
                        await new Commands.Submodule(_repo.FullPath)
                            .Use(log)
                            .UpdateAsync(submodules, true, true);
                }

                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");
            }

            log.Complete();

            if (_repo.HistoriesFilterMode == Models.FilterMode.Included)
                _repo.SetBranchFilterMode(LocalBranch, Models.FilterMode.Included, false, false);

            _repo.MarkBranchesDirtyManually();

            ProgressDescription = "Waiting for branch updated...";
            await Task.Delay(400);
            return succ;
        }

        private Repository _repo;
    }
}
