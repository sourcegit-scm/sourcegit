using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class MergeMultiple : Popup
    {
        public List<object> Targets
        {
            get;
            private set;
        } = [];

        public bool AutoCommit
        {
            get;
            set;
        }

        public Models.MergeStrategy Strategy
        {
            get;
            set;
        }

        public MergeMultiple(Repository repo, List<Models.Commit> commits)
        {
            _repo = repo;
            Targets.AddRange(commits);
            AutoCommit = true;
            Strategy = Models.MergeStrategy.ForMultiple[0];
            View = new Views.MergeMultiple() { DataContext = this };
        }

        public MergeMultiple(Repository repo, List<Models.Branch> branches)
        {
            _repo = repo;
            Targets.AddRange(branches);
            AutoCommit = true;
            Strategy = Models.MergeStrategy.ForMultiple[0];
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
                    ConvertTargetToMergeSources(),
                    AutoCommit,
                    Strategy.Arg,
                    SetProgressDescription).Exec();

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private List<string> ConvertTargetToMergeSources()
        {
            var ret = new List<string>();
            foreach (var t in Targets)
            {
                if (t is Models.Branch branch)
                {
                    ret.Add(branch.FriendlyName);
                }
                else if (t is Models.Commit commit)
                {
                    var d = commit.Decorators.Find(x =>
                    {
                        return x.Type == Models.DecoratorType.LocalBranchHead ||
                            x.Type == Models.DecoratorType.RemoteBranchHead ||
                            x.Type == Models.DecoratorType.Tag;
                    });

                    if (d != null)
                        ret.Add(d.Name);
                    else
                        ret.Add(commit.SHA);
                }
            }

            return ret;
        }

        private readonly Repository _repo = null;
    }
}
