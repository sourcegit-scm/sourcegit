﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPull : Popup
    {
        public List<Models.Remote> Remotes => _repo.Remotes;

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public LFSPull(Repository repo)
        {
            _repo = repo;
            SelectedRemote = _repo.Remotes[0];
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Pull LFS objects from remote ...";

            var log = _repo.CreateLog("LFS Pull");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.LFS(_repo.FullPath).Pull(SelectedRemote.Name, log);
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
