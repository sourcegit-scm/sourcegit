using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CheckoutAndFastForward : Popup
    {
        public Models.Branch LocalBranch
        {
            get;
        }

        public Models.Branch RemoteBrach
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
            RemoteBrach = remoteBranch;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout and Fast-Forward '{LocalBranch.Name}' ...";

            var log = _repo.CreateLog($"Checkout and Fast-Forward '{LocalBranch.Name}' ...");
            Use(log);

            var updateSubmodules = IsRecurseSubmoduleVisible && RecurseSubmodules;
            return Task.Run(() =>
            {
                var succ = false;
                var needPopStash = false;

                if (DiscardLocalChanges)
                {
                    succ = new Commands.Checkout(_repo.FullPath).Use(log).Branch(LocalBranch.Name, RemoteBrach.Head, true, true);
                }
                else
                {
                    var changes = new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).Result();
                    if (changes > 0)
                    {
                        succ = new Commands.Stash(_repo.FullPath).Use(log).Push("CHECKOUT_AND_FASTFORWARD_AUTO_STASH");
                        if (!succ)
                        {
                            log.Complete();
                            CallUIThread(() => _repo.SetWatcherEnabled(true));
                            return false;
                        }

                        needPopStash = true;
                    }

                    succ = new Commands.Checkout(_repo.FullPath).Use(log).Branch(LocalBranch.Name, RemoteBrach.Head, false, true);
                }

                if (succ)
                {
                    if (updateSubmodules)
                    {
                        var submodules = new Commands.QueryUpdatableSubmodules(_repo.FullPath).Result();
                        if (submodules.Count > 0)
                            new Commands.Submodule(_repo.FullPath).Use(log).Update(submodules, true, true);
                    }

                    if (needPopStash)
                        new Commands.Stash(_repo.FullPath).Use(log).Pop("stash@{0}");
                }

                log.Complete();

                CallUIThread(() =>
                {
                    ProgressDescription = "Waiting for branch updated...";

                    if (_repo.HistoriesFilterMode == Models.FilterMode.Included)
                        _repo.SetBranchFilterMode(LocalBranch, Models.FilterMode.Included, true, false);

                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });

                Task.Delay(400).Wait();
                return succ;
            });
        }

        private Repository _repo;
    }
}
