﻿using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeinitSubmodule : Popup
    {
        public string Submodule
        {
            get;
            private set;
        }

        public bool Force
        {
            get;
            set;
        }

        public DeinitSubmodule(Repository repo, string submodule)
        {
            _repo = repo;
            Submodule = submodule;
            Force = false;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "De-initialize Submodule";

            var log = _repo.CreateLog("De-initialize Submodule");
            Use(log);

            var succ = await new Commands.Submodule(_repo.FullPath).Use(log).DeinitAsync(Submodule, false);
            log.Complete();
            await CallUIThreadAsync(() => _repo.SetWatcherEnabled(true));
            return succ;
        }

        private Repository _repo;
    }
}
