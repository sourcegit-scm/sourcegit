﻿using System.Threading.Tasks;

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

        public bool Squash
        {
            get;
            set;
        }

        public bool AutoPush
        {
            get;
            set;
        }

        public bool KeepBranch
        {
            get;
            set;
        }

        public GitFlowFinish(Repository repo, Models.Branch branch, Models.GitFlowBranchType type)
        {
            _repo = repo;
            Branch = branch;
            Type = type;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Git Flow - Finish {Branch.Name} ...";

            var log = _repo.CreateLog("GitFlow - Finish");
            Use(log);

            var prefix = _repo.GitFlow.GetPrefix(Type);
            var name = Branch.Name.StartsWith(prefix) ? Branch.Name.Substring(prefix.Length) : Branch.Name;
            var succ = await Commands.GitFlow.FinishAsync(_repo.FullPath, Type, name, Squash, AutoPush, KeepBranch, log);

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return succ;
        }

        private readonly Repository _repo;
    }
}
