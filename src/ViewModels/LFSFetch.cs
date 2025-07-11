﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSFetch : Popup
    {
        public List<Models.Remote> Remotes => _repo.Remotes;

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public LFSFetch(Repository repo)
        {
            _repo = repo;
            SelectedRemote = _repo.Remotes[0];
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Fetching LFS objects from remote ...";

            var log = _repo.CreateLog("LFS Fetch");
            Use(log);

            await new Commands.LFS(_repo.FullPath)
                .Use(log)
                .FetchAsync(SelectedRemote.Name);

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return true;
        }

        private readonly Repository _repo;
    }
}
