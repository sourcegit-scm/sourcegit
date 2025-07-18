﻿using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class RemoveWorktree : Popup
    {
        public Models.Worktree Target
        {
            get;
        }

        public bool Force
        {
            get;
            set;
        } = false;

        public RemoveWorktree(Repository repo, Models.Worktree target)
        {
            _repo = repo;
            Target = target;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Remove worktree ...";

            var log = _repo.CreateLog("Remove worktree");
            Use(log);

            var succ = await new Commands.Worktree(_repo.FullPath)
                .Use(log)
                .RemoveAsync(Target.FullPath, Force);

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
