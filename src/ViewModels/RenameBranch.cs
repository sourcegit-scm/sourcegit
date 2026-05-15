using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class RenameBranch : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public Models.Branch TrackingRemoteBranch
        {
            get;
        }

        public string RenameRemoteTip
        {
            get;
            private set;
        }

        public bool AlsoRenameRemote
        {
            get => _alsoRenameRemote;
            set => SetProperty(ref _alsoRenameRemote, value);
        }

        public bool CanRenameRemote
        {
            get;
            private set;
        }

        [Required(ErrorMessage = "Branch name is required!!!")]
        [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(RenameBranch), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public RenameBranch(Repository repo, Models.Branch target)
        {
            _repo = repo;
            _name = target.Name;
            Target = target;

            if (target.IsLocal && !string.IsNullOrEmpty(target.Upstream))
            {
                TrackingRemoteBranch = repo.Branches.Find(x => x.FullName == target.Upstream);
                if (TrackingRemoteBranch != null)
                {
                    RenameRemoteTip = App.Text("RenameBranch.WithTrackingRemote", TrackingRemoteBranch.FriendlyName);
                    CanRenameRemote = target.Head == TrackingRemoteBranch.Head;
                }
            }
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is RenameBranch rename)
            {
                foreach (var b in rename._repo.Branches)
                {
                    if (b.IsLocal && b != rename.Target && b.Name.Equals(name, StringComparison.Ordinal))
                        return new ValidationResult("A branch with same name already exists!!!");
                }
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            if (Target.Name.Equals(_name, StringComparison.Ordinal))
                return true;

            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Rename '{Target.Name}'";

            var log = _repo.CreateLog($"Rename Branch '{Target.Name}'");
            Use(log);

            var oldName = Target.Name;

            var succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                .Use(log)
                .RenameAsync(_name);

            if (succ)
            {
                _repo.RefreshAfterRenameBranch(Target, _name);

                if (_alsoRenameRemote && TrackingRemoteBranch != null)
                {
                    var remote = TrackingRemoteBranch.Remote;
                    var pushNew = new Commands.Push(_repo.FullPath, TrackingRemoteBranch.Head, remote, $"refs/heads/{_name}", false, false, false, false);
                    pushNew.Use(log);
                    await pushNew.RunAsync();

                    var deleteOld = new Commands.Push(_repo.FullPath, remote, $"refs/heads/{TrackingRemoteBranch.Name}", true);
                    deleteOld.Use(log);
                    await deleteOld.RunAsync();

                    // Update local branch's tracking to the new remote branch
                    var newUpstream = new Models.Branch
                    {
                        Name = _name,
                        FullName = $"refs/remotes/{remote}/{_name}",
                        Remote = remote,
                        IsLocal = false,
                    };
                    await new Commands.Branch(_repo.FullPath, _name)
                        .Use(log)
                        .SetUpstreamAsync(newUpstream);
                }
            }

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private string _name;
        private bool _alsoRenameRemote = false;
    }
}
