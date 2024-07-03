using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Checkout : Popup
    {
        public string Branch
        {
            get;
            private set;
        }

        public Models.DealWithLocalChanges PreAction
        {
            get => _repo.Settings.DealWithLocalChangesOnCheckoutBranch;
            set => _repo.Settings.DealWithLocalChangesOnCheckoutBranch = value;
        }

        public Checkout(Repository repo, string branch)
        {
            _repo = repo;
            Branch = branch;
            View = new Views.Checkout() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout '{Branch}' ...";

            var hasLocalChanges = _repo.WorkingCopyChangesCount > 0;
            return Task.Run(() =>
            {
                var needPopStash = false;
                if (hasLocalChanges)
                {
                    if (PreAction == Models.DealWithLocalChanges.StashAndReaply)
                    {
                        SetProgressDescription("Adding untracked changes ...");
                        var succ = new Commands.Add(_repo.FullPath).Exec();
                        if (succ)
                        {
                            SetProgressDescription("Stash local changes ...");
                            succ = new Commands.Stash(_repo.FullPath).Push("CHECKOUT_AUTO_STASH");
                        }

                        if (!succ)
                        {
                            CallUIThread(() => _repo.SetWatcherEnabled(true));
                            return false;
                        }

                        needPopStash = true;
                    }
                    else if (PreAction == Models.DealWithLocalChanges.Discard)
                    {
                        SetProgressDescription("Discard local changes ...");
                        Commands.Discard.All(_repo.FullPath);
                    }
                }

                SetProgressDescription("Checkout branch ...");
                var rs = new Commands.Checkout(_repo.FullPath).Branch(Branch, SetProgressDescription);

                if (needPopStash)
                {
                    SetProgressDescription("Re-apply local changes...");
                    rs = new Commands.Stash(_repo.FullPath).Apply("stash@{0}");
                    if (rs)
                    {
                        rs = new Commands.Stash(_repo.FullPath).Drop("stash@{0}");
                    }
                }

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return rs;
            });
        }

        private readonly Repository _repo = null;
    }
}
