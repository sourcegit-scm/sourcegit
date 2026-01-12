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

        public CheckoutCommit(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            Commit = commit;
            DiscardLocalChanges = false;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
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
                        return true;
                }
            }

            var succ = false;
            var needPop = false;

            if (!DiscardLocalChanges)
            {
                var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AUTO_STASH", false);
                    if (!succ)
                    {
                        log.Complete();
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
                var submodules = await new Commands.QueryUpdatableSubmodules(_repo.FullPath, false).GetResultAsync();
                if (submodules.Count > 0)
                    await new Commands.Submodule(_repo.FullPath)
                        .Use(log)
                        .UpdateAsync(submodules, false, true);

                if (needPop)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");
            }

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
