﻿using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPrune : Popup
    {
        public LFSPrune(Repository repo)
        {
            _repo = repo;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "LFS prune ...";

            var log = _repo.CreateLog("LFS Prune");
            Use(log);

            await new Commands.LFS(_repo.FullPath).PruneAsync(log);
            log.Complete();
            await CallUIThreadAsync(() => _repo.SetWatcherEnabled(true));
            return true;
        }

        private readonly Repository _repo = null;
    }
}
