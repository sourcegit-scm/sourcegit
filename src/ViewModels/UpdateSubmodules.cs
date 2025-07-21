using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class UpdateSubmodules : Popup
    {
        public bool HasPreSelectedSubmodule
        {
            get;
        }

        public List<Models.Submodule> Submodules
        {
            get => _repo.Submodules;
        }

        public Models.Submodule SelectedSubmodule
        {
            get;
            set;
        }

        public bool UpdateAll
        {
            get => _updateAll;
            set => SetProperty(ref _updateAll, value);
        }

        public bool IsEnableInitVisible
        {
            get;
            set;
        } = true;

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

        public UpdateSubmodules(Repository repo, Models.Submodule selected)
        {
            _repo = repo;

            if (selected != null)
            {
                _updateAll = false;
                SelectedSubmodule = selected;
                IsEnableInitVisible = selected.Status == Models.SubmoduleStatus.NotInited;
                EnableInit = selected.Status == Models.SubmoduleStatus.NotInited;
                HasPreSelectedSubmodule = true;
            }
            else if (repo.Submodules.Count > 0)
            {
                SelectedSubmodule = repo.Submodules[0];
                IsEnableInitVisible = true;
                HasPreSelectedSubmodule = false;
            }
        }

        public override async Task<bool> Sure()
        {
            var targets = new List<string>();
            if (_updateAll)
            {
                foreach (var submodule in Submodules)
                    targets.Add(submodule.Path);
            }
            else if (SelectedSubmodule != null)
            {
                targets.Add(SelectedSubmodule.Path);
            }

            if (targets.Count == 0)
                return true;

            var log = _repo.CreateLog("Update Submodule");
            _repo.SetWatcherEnabled(false);
            Use(log);

            await new Commands.Submodule(_repo.FullPath)
                .Use(log)
                .UpdateAsync(targets, EnableInit, EnableRecursive, EnableRemote);

            log.Complete();
            _repo.SetWatcherEnabled(true);
            return true;
        }

        private readonly Repository _repo = null;
        private bool _updateAll = true;
    }
}
