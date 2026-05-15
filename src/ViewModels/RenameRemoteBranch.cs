using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class RenameRemoteBranch : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        [Required(ErrorMessage = "Branch name is required!!!")]
        [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public RenameRemoteBranch(Repository repo, Models.Branch target)
        {
            _repo = repo;
            Target = target;
            _name = target.Name;
        }

        public override async Task<bool> Sure()
        {
            if (Target.Name.Equals(_name, StringComparison.Ordinal))
                return true;

            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Rename remote branch '{Target.FriendlyName}'";

            var log = _repo.CreateLog($"Rename Remote Branch '{Target.FriendlyName}'");
            Use(log);

            var remote = Target.Remote;
            var succ = false;

            // Push the current commit to the new branch name on the remote
            var pushNew = new Commands.Push(_repo.FullPath, Target.Head, remote, $"refs/heads/{_name}", false, false, false, false);
            pushNew.Use(log);
            var pushOk = await pushNew.RunAsync();

            if (pushOk)
            {
                // Delete the old remote branch
                var deleteOld = new Commands.Push(_repo.FullPath, remote, $"refs/heads/{Target.Name}", true);
                deleteOld.Use(log);
                succ = await deleteOld.RunAsync();
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return succ;
        }

        private readonly Repository _repo;
        private string _name;
    }
}
