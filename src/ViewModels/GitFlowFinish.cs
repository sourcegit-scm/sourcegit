using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowFinish : Popup
    {
        public Models.Branch Branch
        {
            get;
            set;
        } = null;

        public bool IsFeature => _type == "feature";
        public bool IsRelease => _type == "release";
        public bool IsHotfix => _type == "hotfix";

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
            View = new Views.GitFlowFinish() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() =>
            {
                var name = Branch.Name.StartsWith(_prefix) ? Branch.Name.Substring(_prefix.Length) : Branch.Name;
                SetProgressDescription($"Git Flow - finishing {_type} {name} ...");
                var succ = Commands.GitFlow.Finish(_repo.FullPath, _type, name, KeepBranch);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private readonly string _type = "feature";
        private readonly string _prefix = string.Empty;
    }
}
