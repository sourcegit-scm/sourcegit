using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PushTag : Popup
    {
        public Models.Tag Target
        {
            get;
        }

        public List<Models.Remote> Remotes
        {
            get => _repo.Remotes;
        }

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public bool PushAllRemotes
        {
            get => _pushAllRemotes;
            set => SetProperty(ref _pushAllRemotes, value);
        }

        public PushTag(Repository repo, Models.Tag target)
        {
            _repo = repo;
            Target = target;
            SelectedRemote = _repo.Remotes[0];
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Pushing tag ...";

            var log = _repo.CreateLog("Push Tag");
            Use(log);

            var succ = true;
            var tag = $"refs/tags/{Target.Name}";
            if (_pushAllRemotes)
            {
                foreach (var remote in _repo.Remotes)
                {
                    succ = await new Commands.Push(_repo.FullPath, remote.Name, tag, false)
                        .Use(log)
                        .RunAsync();
                    if (!succ)
                        break;
                }
            }
            else
            {
                succ = await new Commands.Push(_repo.FullPath, SelectedRemote.Name, tag, false)
                    .Use(log)
                    .RunAsync();
            }

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
        private bool _pushAllRemotes = false;
    }
}
