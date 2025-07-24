using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class SetUpstream : Popup
    {
        public Models.Branch Local
        {
            get;
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
        }

        public override async Task<bool> Sure()
        {
            ProgressDescription = "Setting upstream...";
            Models.Branch upstream = _unset ? null : SelectedRemoteBranch;

            if (upstream == null)
            {
                if (string.IsNullOrEmpty(Local.Upstream))
                    return true;
            }
            else if (upstream.FullName.Equals(Local.Upstream, StringComparison.Ordinal))
            {
                return true;
            }

            var log = _repo.CreateLog("Set Upstream");
            Use(log);

            var succ = await new Commands.Branch(_repo.FullPath, Local.Name)
                .Use(log)
                .SetUpstreamAsync(upstream);

            log.Complete();
            if (succ)
                _repo.MarkBranchesDirtyManually();
            return true;
        }

        private readonly Repository _repo;
        private bool _unset = false;
    }
}
