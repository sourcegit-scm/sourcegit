using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class MoveSubmodule : Popup
    {
        public Models.Submodule Submodule
        {
            get;
        }

        [Required(ErrorMessage = "Path is required!!!")]
        public string MoveTo
        {
            get => _moveTo;
            set => SetProperty(ref _moveTo, value, true);
        }

        public MoveSubmodule(Repository repo, Models.Submodule submodule)
        {
            _repo = repo;
            _moveTo = submodule.Path;
            Submodule = submodule;
        }

        public override async Task<bool> Sure()
        {
            ProgressDescription = "Moving submodule ...";

            var oldPath = Native.OS.GetAbsPath(_repo.FullPath, Submodule.Path);
            var newPath = Native.OS.GetAbsPath(_repo.FullPath, _moveTo);
            if (oldPath.Equals(newPath, StringComparison.Ordinal))
                return true;

            using var lockWatcher = _repo.LockWatcher();
            var log = _repo.CreateLog("Move Submodule");
            Use(log);

            var succ = await new Commands.Move(_repo.FullPath, oldPath, newPath, false)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return succ;
        }

        private Repository _repo;
        private string _moveTo;
    }
}
