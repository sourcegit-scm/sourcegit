using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowFinish : Popup
    {
        public Models.Branch Branch
        {
            get;
        }

        public bool IsFeature => _type == "feature";
        public bool IsRelease => _type == "release";
        public bool IsHotfix => _type == "hotfix";

        public bool Squash
        {
            get;
            set;
        } = false;

        public bool AutoPush
        {
            get;
            set;
        } = false;

        public bool KeepBranch
        {
            get;
            set;
        } = false;

        public GitFlowFinish(Repository repo, Models.Branch branch, string type, string prefix)
        {
            _repo = repo;
            _type = type;
            _prefix = prefix;
            Branch = branch;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            var name = Branch.Name.StartsWith(_prefix) ? Branch.Name.Substring(_prefix.Length) : Branch.Name;
            ProgressDescription = $"Git Flow - finishing {_type} {name} ...";

            var log = _repo.CreateLog("Gitflow - Finish");
            Use(log);

            return Task.Run(() =>
            {
                var succ = Commands.GitFlow.Finish(_repo.FullPath, _type, name, Squash, AutoPush, KeepBranch, log);
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo;
        private readonly string _type;
        private readonly string _prefix;
    }
}
