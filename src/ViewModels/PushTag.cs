using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PushTag : Popup
    {
        public Models.Tag Target
        {
            get;
            private set;
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
            View = new Views.PushTag() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Pushing tag ...";

            return Task.Run(() =>
            {
                bool succ = true;
                if (_pushAllRemotes)
                {
                    foreach (var remote in _repo.Remotes)
                    {
                        SetProgressDescription($"Pushing tag to remote {remote.Name} ...");
                        succ = new Commands.Push(_repo.FullPath, remote.Name, Target.Name, false).Exec();
                        if (!succ)
                            break;
                    }
                }
                else
                {
                    SetProgressDescription($"Pushing tag to remote {SelectedRemote.Name} ...");
                    succ = new Commands.Push(_repo.FullPath, SelectedRemote.Name, Target.Name, false).Exec();
                }

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private bool _pushAllRemotes = false;
    }
}
