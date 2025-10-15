using System;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PushRevision : Popup
    {
        public Models.Commit Revision
        {
            get;
        }

        public Models.Branch RemoteBranch
        {
            get;
        }

        public bool Force
        {
            get;
            set;
        }

        public PushRevision(Repository repo, Models.Commit revision, Models.Branch remoteBranch)
        {
            _repo = repo;
            Revision = revision;
            RemoteBranch = remoteBranch;
            Force = false;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Push {Revision.SHA.AsSpan(0, 10)} -> {RemoteBranch.FriendlyName} ...";

            var log = _repo.CreateLog("Push Revision");
            Use(log);

            var succ = await new Commands.Push(
                _repo.FullPath,
                Revision.SHA,
                RemoteBranch.Remote,
                RemoteBranch.Name,
                false,
                false,
                false,
                Force).Use(log).RunAsync();

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
    }
}
