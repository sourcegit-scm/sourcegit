using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class SetUpstream : Popup
    {
        public Models.Branch Local
        {
            get;
            private set;
        }

        public List<Models.Branch> RemoteBranches
        {
            get;
            private set;
        }

        public Models.Branch SelectedRemoteBranch
        {
            get;
            set;
        }

        public bool Unset
        {
            get => _unset;
            set => SetProperty(ref _unset, value);
        }

        public SetUpstream(Repository repo, Models.Branch local, List<Models.Branch> remoteBranches)
        {
            _repo = repo;
            Local = local;
            RemoteBranches = remoteBranches;
            Unset = false;

            if (!string.IsNullOrEmpty(local.Upstream))
            {
                var upstream = remoteBranches.Find(x => x.FullName == local.Upstream);
                if (upstream != null)
                    SelectedRemoteBranch = upstream;
            }

            if (SelectedRemoteBranch == null)
            {
                var upstream = remoteBranches.Find(x => x.Name == local.Name);
                if (upstream != null)
                    SelectedRemoteBranch = upstream;
            }

            View = new Views.SetUpstream() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            SetProgressDescription("Setting upstream...");

            var upstream = (_unset || SelectedRemoteBranch == null) ? string.Empty : SelectedRemoteBranch.FullName;
            if (upstream == Local.Upstream)
                return null;

            return Task.Run(() =>
            {
                var succ = Commands.Branch.SetUpstream(_repo.FullPath, Local.Name, upstream.Replace("refs/remotes/", ""));
                if (succ)
                    _repo.RefreshBranches();
                return true;
            });
        }

        private Repository _repo;
        private bool _unset = false;
    }
}
