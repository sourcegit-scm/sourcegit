using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowFinish : Popup
    {
        public Models.Branch Branch
        {
            get;
        }

        public Models.GitFlowBranchType Type
        {
            get;
            private set;
        }

        public bool RebaseBeforeMerging
        {
            get;
            set;
        } = false;

        public bool Squash
        {
            get;
            set;
        } = false;

        public bool KeepBranch
        {
            get;
            set;
        } = false;

        public GitFlowFinish(Repository repo, Models.Branch branch, Models.GitFlowBranchType type)
        {
            _repo = repo;
            Branch = branch;
            Type = type;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Git Flow - Finish {Branch.Name} ...";

            var log = _repo.CreateLog("GitFlow - Finish");
            Use(log);

            var prefix = _repo.GitFlow.GetPrefix(Type);
            var name = Branch.Name.StartsWith(prefix) ? Branch.Name.Substring(prefix.Length) : Branch.Name;
            var succ = await new Commands.GitFlow(_repo.FullPath)
                .Use(log)
                .FinishAsync(Type, name, RebaseBeforeMerging, Squash, KeepBranch);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
    }
}
