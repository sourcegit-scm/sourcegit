using System.Collections.Generic;
using System.Threading.Tasks;
using SourceGit.Models;

namespace SourceGit.ViewModels
{
    public class MergeMultiple : Popup
    {
        public List<string> Strategies = ["octopus", "ours"];

        public List<Commit> Targets
        {
            get;
            private set;
        }

        public bool AutoCommit
        {
            get;
            set;
        }

        public MergeStrategy Strategy
        {
            get;
            set;
        }

        public MergeMultiple(Repository repo, List<Commit> targets)
        {
            _repo = repo;
            Targets = targets;
            AutoCommit = true;
            Strategy = MergeStrategy.ForMultiple.Find(s => s.Arg == null);
            View = new Views.MergeMultiple() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Merge head(s) ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Merge(
                    _repo.FullPath,
                    string.Join(" ", Targets.ConvertAll(c => c.Decorators.Find(d => d.Type == DecoratorType.RemoteBranchHead || d.Type == DecoratorType.LocalBranchHead)?.Name ?? c.Decorators.Find(d => d.Type == DecoratorType.Tag)?.Name ?? c.SHA)),
                    AutoCommit ? string.Empty : "--no-commit",
                    Strategy?.Arg,
                    SetProgressDescription).Exec();

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}
