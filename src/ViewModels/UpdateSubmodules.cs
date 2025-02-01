using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class UpdateSubmodules : Popup
    {
        public List<string> Submodules
        {
            get;
            private set;
        } = new List<string>();

        public string SelectedSubmodule
        {
            get;
            set;
        }

        public bool UpdateAll
        {
            get => _updateAll;
            set => SetProperty(ref _updateAll, value);
        }

        public bool EnableInit
        {
            get;
            set;
        } = true;

        public bool EnableRecursive
        {
            get;
            set;
        } = true;

        public bool EnableRemote
        {
            get;
            set;
        } = false;

        public UpdateSubmodules(Repository repo)
        {
            _repo = repo;

            foreach (var submodule in _repo.Submodules)
                Submodules.Add(submodule.Path);

            SelectedSubmodule = Submodules.Count > 0 ? Submodules[0] : string.Empty;
            View = new Views.UpdateSubmodules() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);

            List<string> targets;
            if (_updateAll)
                targets = Submodules;
            else
                targets = [SelectedSubmodule];

            return Task.Run(() =>
            {
                foreach (var submodule in targets)
                {
                    ProgressDescription = $"Updating submodule {submodule} ...";
                    new Commands.Submodule(_repo.FullPath).Update(
                        submodule,
                        EnableInit,
                        EnableRecursive,
                        EnableRemote,
                        SetProgressDescription);
                }

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
        private bool _updateAll = true;
    }
}
