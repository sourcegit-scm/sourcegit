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

        public Checkout(Repository repo, string branch)
        {
            _repo = repo;
            Branch = branch;
            DiscardLocalChanges = false;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout '{Branch}' ...";

            var log = _repo.CreateLog($"Checkout '{Branch}'");
            Use(log);

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

                var rs = new Commands.Checkout(_repo.FullPath).Use(log).Branch(Branch);
                if (needPopStash)
                    rs = new Commands.Stash(_repo.FullPath).Use(log).Pop("stash@{0}");

                log.Complete();

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
