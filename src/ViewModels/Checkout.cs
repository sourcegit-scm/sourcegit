using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Checkout : Popup
    {
        public string BranchName
        {
            get => _branch.Name;
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

        public Checkout(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _branch = branch;

            DealWithLocalChanges = Preferences.Instance.UseStashAndReapplyByDefault ?
                Models.DealWithLocalChanges.StashAndReapply :
                Models.DealWithLocalChanges.DoNothing;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            var branchName = BranchName;
            ProgressDescription = $"Checkout '{branchName}' ...";

            var log = _repo.CreateLog($"Checkout '{branchName}'");
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
                    .BranchAsync(branchName, false);
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

                    needPopStash = true;
                }

                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .BranchAsync(branchName, false);
            }
            else
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .BranchAsync(branchName, true);
            }

            if (succ)
            {
                await _repo.AutoUpdateSubmodulesAsync(log);

                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");

                _repo.RefreshAfterCheckoutBranch(_branch);
            }
            else
            {
                _repo.MarkWorkingCopyDirtyManually();
            }

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
        private readonly Models.Branch _branch = null;
    }
}
