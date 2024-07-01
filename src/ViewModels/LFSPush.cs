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
            View = new Views.LFSPush() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Push LFS objects to remote ...";
            return Task.Run(() =>
            {
                new Commands.LFS(_repo.FullPath).Push(SelectedRemote.Name, SetProgressDescription);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
    }
}
