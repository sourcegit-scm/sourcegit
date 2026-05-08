using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CheckoutCommit : Popup
    {
        public Models.Commit Commit
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

        public CheckoutCommit(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            Commit = commit;

            DealWithLocalChanges = Preferences.Instance.UseStashAndReapplyByDefault ?
                Models.DealWithLocalChanges.StashAndReapply :
                Models.DealWithLocalChanges.DoNothing;
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

            if (DealWithLocalChanges == Models.DealWithLocalChanges.DoNothing)
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .CommitAsync(Commit.SHA, false);
            }
            else if (DealWithLocalChanges == Models.DealWithLocalChanges.StashAndReapply)
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
                        _repo.MarkWorkingCopyDirtyManually();
                        return false;
                    }

                    needPop = true;
                }

                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .CommitAsync(Commit.SHA, false);
            }
            else
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .CommitAsync(Commit.SHA, true);
            }

            if (succ)
            {
                await _repo.AutoUpdateSubmodulesAsync(log);

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
