﻿using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Rebase : Popup
    {
        public Models.Branch Current
        {
            get;
            private set;
        }

        public object On
        {
            get;
            private set;
        }

        public bool AutoStash
        {
            get;
            set;
        }

        public Rebase(Repository repo, Models.Branch current, Models.Branch on)
        {
            _repo = repo;
            _revision = on.Head;
            Current = current;
            On = on;
            AutoStash = true;
        }

        public Rebase(Repository repo, Models.Branch current, Models.Commit on)
        {
            _repo = repo;
            _revision = on.SHA;
            Current = current;
            On = on;
            AutoStash = true;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            _repo.ClearCommitMessage();
            ProgressDescription = "Rebasing ...";

            var log = _repo.CreateLog("Rebase");
            Use(log);

            return Task.Run(async () =>
            {
                await new Commands.Rebase(_repo.FullPath, _revision, AutoStash).Use(log).ExecAsync();
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo;
        private readonly string _revision;
    }
}
