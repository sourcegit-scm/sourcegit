﻿using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class RemoveWorktree : Popup
    {
        public Models.Worktree Target
        {
            get;
            private set;
        } = null;

        public bool Force
        {
            get;
            set;
        } = false;

        public RemoveWorktree(Repository repo, Models.Worktree target)
        {
            _repo = repo;
            Target = target;
            View = new Views.RemoveWorktree() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Remove worktree ...";

            var log = _repo.CreateLog("Remove worktree");
            Use(log);

            return Task.Run(() =>
            {
                var succ = new Commands.Worktree(_repo.FullPath).Use(log).Remove(Target.FullPath, Force);
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
    }
}
