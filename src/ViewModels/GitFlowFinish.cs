using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowFinish : Popup
    {
        public Models.Branch Branch => _branch;
        public bool IsFeature => _type == Models.GitFlowBranchType.Feature;
        public bool IsRelease => _type == Models.GitFlowBranchType.Release;
        public bool IsHotfix => _type == Models.GitFlowBranchType.Hotfix;

        public bool KeepBranch
        {
            get;
            set;
        } = false;

        public GitFlowFinish(Repository repo, Models.Branch branch, Models.GitFlowBranchType type)
        {
            _repo = repo;
            _branch = branch;
            _type = type;
            View = new Views.GitFlowFinish() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() =>
            {
                var branch = _branch.Name;
                switch (_type)
                {
                    case Models.GitFlowBranchType.Feature:
                        branch = branch.Substring(_repo.GitFlow.Feature.Length);
                        break;
                    case Models.GitFlowBranchType.Release:
                        branch = branch.Substring(_repo.GitFlow.Release.Length);
                        break;
                    default:
                        branch = branch.Substring(_repo.GitFlow.Hotfix.Length);
                        break;
                }

                SetProgressDescription($"Git Flow - finishing {_branch.Name} ...");
                var succ = new Commands.GitFlow(_repo.FullPath).Finish(_type, branch, KeepBranch);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private readonly Models.Branch _branch = null;
        private readonly Models.GitFlowBranchType _type = Models.GitFlowBranchType.None;
    }
}
