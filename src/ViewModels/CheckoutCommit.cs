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

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout Commit '{Commit.SHA}' ...";

            var log = _repo.CreateLog("Checkout Commit");
            Use(log);

            var updateSubmodules = IsRecurseSubmoduleVisible && RecurseSubmodules;
            return Task.Run(() =>
            {
                var succ = false;
                var needPop = false;

                if (DiscardLocalChanges)
                {
                    succ = new Commands.Checkout(_repo.FullPath).Use(log).Commit(Commit.SHA, true);
                }
                else
                {
                    var changes = new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).Result();
                    if (changes > 0)
                    {
                        succ = new Commands.Stash(_repo.FullPath).Use(log).Push("CHECKOUT_AUTO_STASH");
                        if (!succ)
                        {
                            log.Complete();
                            CallUIThread(() => _repo.SetWatcherEnabled(true));
                            return false;
                        }

                        needPop = true;
                    }

                    succ = new Commands.Checkout(_repo.FullPath).Use(log).Commit(Commit.SHA, false);
                }

                if (succ)
                {
                    if (updateSubmodules)
                    {
                        var submodules = new Commands.QueryUpdatableSubmodules(_repo.FullPath).Result();
                        if (submodules.Count > 0)
                            new Commands.Submodule(_repo.FullPath).Use(log).Update(submodules, true, true);
                    }

                    if (needPop)
                        new Commands.Stash(_repo.FullPath).Use(log).Pop("stash@{0}");
                }

                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
