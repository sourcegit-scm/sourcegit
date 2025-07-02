using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSPush : Popup
    {
        public List<Models.Remote> Remotes => _repo.Remotes;

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public LFSPush(Repository repo)
        {
            _repo = repo;
            SelectedRemote = _repo.Remotes[0];
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Push LFS objects to remote ...";

            var log = _repo.CreateLog("LFS Push");
            Use(log);

            await new Commands.LFS(_repo.FullPath).PushAsync(SelectedRemote.Name, log);
            log.Complete();
            await CallUIThreadAsync(() => _repo.SetWatcherEnabled(true));
            return true;
        }

        private readonly Repository _repo = null;
    }
}
