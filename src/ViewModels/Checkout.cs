using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Checkout : Popup
    {
        public string Branch
        {
            get;
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

            return Task.Run(() =>
            {
                var changes = new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).Result();
                var needPopStash = false;
                if (changes > 0)
                {
                    if (PreAction == Models.DealWithLocalChanges.StashAndReaply)
                    {
                        SetProgressDescription("Stash local changes ...");
                        var succ = new Commands.Stash(_repo.FullPath).Push("CHECKOUT_AUTO_STASH");
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
                        Commands.Discard.All(_repo.FullPath, false);
                    }
                }

                SetProgressDescription("Checkout branch ...");
                var rs = new Commands.Checkout(_repo.FullPath).Branch(Branch, SetProgressDescription);

                if (needPopStash)
                {
                    SetProgressDescription("Re-apply local changes...");
                    rs = new Commands.Stash(_repo.FullPath).Pop("stash@{0}");
                }

                CallUIThread(() =>
                {
                    var b = _repo.Branches.Find(x => x.IsLocal && x.Name == Branch);
                    if (b != null && _repo.HistoriesFilterMode == Models.FilterMode.Included)
                        _repo.SetBranchFilterMode(b, Models.FilterMode.Included, true, false);

                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });

                return rs;
            });
        }

        private readonly Repository _repo = null;
    }
}
