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
                var changes = new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).Result();
                var needPopStash = false;
                if (changes > 0)
                {
                    if (DiscardLocalChanges)
                    {
                        Commands.Discard.All(_repo.FullPath, false, log);
                    }
                    else
                    {
                        var succ = new Commands.Stash(_repo.FullPath).Use(log).Push("CHECKOUT_AUTO_STASH");
                        if (!succ)
                        {
                            log.Complete();
                            CallUIThread(() => _repo.SetWatcherEnabled(true));
                            return false;
                        }

                        needPopStash = true;
                    }
                }

                var rs = new Commands.Checkout(_repo.FullPath).Use(log).Branch(Branch, updateSubmodules);
                if (needPopStash)
                    rs = new Commands.Stash(_repo.FullPath).Use(log).Pop("stash@{0}");

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
                return rs;
            });
        }

        private readonly Repository _repo = null;
    }
}
