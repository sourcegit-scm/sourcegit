﻿using System.Linq;
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
            get => _repo.Submodules.Count > 0;
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
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Checkout '{Branch}' ...";

            var log = _repo.CreateLog($"Checkout '{Branch}'");
            Use(log);

            if (_repo.CurrentBranch is { IsDetachedHead: true })
            {
                var refs = await new Commands.QueryRefsContainsCommit(_repo.FullPath, _repo.CurrentBranch.Head).GetResultAsync();
                if (refs.Count == 0)
                {
                    var msg = App.Text("Checkout.WarnLostCommits");
                    var shouldContinue = await App.AskConfirmAsync(msg, null);
                    if (!shouldContinue)
                    {
                        _repo.SetWatcherEnabled(true);
                        return true;
                    }
                }
            }

            var succ = false;
            var needPopStash = false;

            if (!DiscardLocalChanges)
            {
                var changes = await new Commands.CountLocalChangesWithoutUntracked(_repo.FullPath).GetResultAsync();
                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AUTO_STASH");
                    if (!succ)
                    {
                        log.Complete();
                        _repo.SetWatcherEnabled(true);
                        return false;
                    }

                    needPopStash = true;
                }
            }

            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .BranchAsync(Branch, DiscardLocalChanges);

            if (succ)
            {
                if (IsRecurseSubmoduleVisible && RecurseSubmodules)
                {
                    var submodules = await new Commands.QueryUpdatableSubmodules(_repo.FullPath).GetResultAsync();
                    if (submodules.Count > 0)
                        await new Commands.Submodule(_repo.FullPath)
                            .Use(log)
                            .UpdateAsync(submodules, true, true);
                }

                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");
            }

            log.Complete();

            var b = _repo.Branches.Find(x => x.IsLocal && x.Name == Branch);
            if (b != null && _repo.HistoriesFilterMode == Models.FilterMode.Included
                && !_repo.Settings.HistoriesFilters.Any(f => f.Pattern == "HEAD"))
                _repo.SetBranchFilterMode(b, Models.FilterMode.Included, true, false);

            _repo.MarkBranchesDirtyManually();
            _repo.SetWatcherEnabled(true);

            ProgressDescription = "Waiting for branch updated...";
            await Task.Delay(400);
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
