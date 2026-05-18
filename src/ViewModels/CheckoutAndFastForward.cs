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

        public bool HasLocalChanges
        {
            get => _repo.LocalChangesCount > 0;
        }

        public Models.DealWithLocalChanges DealWithLocalChanges
        {
            get;
            set;
        }

        public CheckoutAndFastForward(Repository repo, Models.Branch localBranch, Models.Branch remoteBranch)
        {
            _repo = repo;
            LocalBranch = localBranch;
            RemoteBranch = remoteBranch;

            DealWithLocalChanges = Preferences.Instance.UseStashAndReapplyByDefault ?
                Models.DealWithLocalChanges.StashAndReapply :
                Models.DealWithLocalChanges.DoNothing;
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

            if (DealWithLocalChanges == Models.DealWithLocalChanges.DoNothing)
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .BranchAsync(LocalBranch.Name, RemoteBranch.Head, false, true);
            }
            else if (DealWithLocalChanges == Models.DealWithLocalChanges.StashAndReapply)
            {
                var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AND_FASTFORWARD_AUTO_STASH", false);
                    if (!succ)
                    {
                        log.Complete();
                        _repo.MarkWorkingCopyDirtyManually();
                        return false;
                    }

                    needPopStash = true;
                }

                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .BranchAsync(LocalBranch.Name, RemoteBranch.Head, false, true);
            }
            else
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .BranchAsync(LocalBranch.Name, RemoteBranch.Head, true, true);
            }

            if (succ)
            {
                await _repo.AutoUpdateSubmodulesAsync(log);

                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");

                LocalBranch.Behind.Clear();
                LocalBranch.Head = RemoteBranch.Head;
                LocalBranch.CommitterDate = RemoteBranch.CommitterDate;

                _repo.RefreshAfterCheckoutBranch(LocalBranch);
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
            }

            log.Complete();
            return succ;
        }

        private Repository _repo;
    }
}
