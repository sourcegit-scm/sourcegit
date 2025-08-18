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

            if (_repo.CurrentBranch is { IsDetachedHead: true })
            {
                var refs = await new Commands.QueryRefsContainsCommit(_repo.FullPath, _repo.CurrentBranch.Head).GetResultAsync();
                if (refs.Count == 0)
                {
                    var msg = App.Text("Checkout.WarnLostCommits");
                    var shouldContinue = await App.AskConfirmAsync(msg);
                    if (!shouldContinue)
                    {
                        _repo.SetWatcherEnabled(true);
                        return true;
                    }
                }
            }

            var succ = false;
            var needPop = false;

            if (!DiscardLocalChanges)
            {
                var changes = await new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).GetResultAsync();
                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AUTO_STASH");
                    if (!succ)
                    {
                        log.Complete();
                        _repo.SetWatcherEnabled(true);
                        return false;
                    }

                    needPop = true;
                }
            }

            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .CommitAsync(Commit.SHA, DiscardLocalChanges);

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

                if (needPop)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");
            }

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
