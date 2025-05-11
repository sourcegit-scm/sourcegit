using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Checkout : Popup
    {
        public string Branch
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
            get;
            private set;
        }

        public bool RecurseSubmodules
        {
            get => _repo.Settings.UpdateSubmodulesOnCheckoutBranch;
            set => _repo.Settings.UpdateSubmodulesOnCheckoutBranch = value;
        }

        public Checkout(Repository repo, string branch)
        {
            _repo = repo;
            Branch = branch;
            DiscardLocalChanges = false;
            IsRecurseSubmoduleVisible = repo.Submodules.Count > 0;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout '{Branch}' ...";

            var log = _repo.CreateLog($"Checkout '{Branch}'");
            Use(log);

            var updateSubmodules = IsRecurseSubmoduleVisible && RecurseSubmodules;
            return Task.Run(() =>
            {
                var succ = false;
                var needPopStash = false;

                if (DiscardLocalChanges)
                {
                    succ = new Commands.Checkout(_repo.FullPath).Use(log).Branch(Branch, true);
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

                        needPopStash = true;
                    }

                    succ = new Commands.Checkout(_repo.FullPath).Use(log).Branch(Branch, false);
                }

                if (succ)
                {
                    if (updateSubmodules)
                    {
                        var submodules = new Commands.QuerySubmodules(_repo.FullPath).Result();
                        if (submodules.Count > 0)
                            new Commands.Submodule(_repo.FullPath).Use(log).Update(submodules, true, true, false);
                    }

                    if (needPopStash)
                        new Commands.Stash(_repo.FullPath).Use(log).Pop("stash@{0}");
                }

                log.Complete();

                CallUIThread(() =>
                {
                    ProgressDescription = "Waiting for branch updated...";

                    var b = _repo.Branches.Find(x => x.IsLocal && x.Name == Branch);
                    if (b != null && _repo.HistoriesFilterMode == Models.FilterMode.Included)
                        _repo.SetBranchFilterMode(b, Models.FilterMode.Included, true, false);

                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });

                Task.Delay(400).Wait();
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
