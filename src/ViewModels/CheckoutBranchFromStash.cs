using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CheckoutBranchFromStash : Popup
    {
        public Models.Stash Target
        {
            get;
        }

        [Required(ErrorMessage = "Branch name is required!")]
        [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(CheckoutBranchFromStash), nameof(ValidateBranchName))]
        public string BranchName
        {
            get => _branchName;
            set => SetProperty(ref _branchName, value, true);
        }

        public CheckoutBranchFromStash(Repository repo, Models.Stash stash)
        {
            _repo = repo;
            Target = stash;
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is CheckoutBranchFromStash caller)
            {
                foreach (var b in caller._repo.Branches)
                {
                    if (b.FriendlyName.Equals(name, StringComparison.Ordinal))
                        return new ValidationResult("A branch with same name already exists!");
                }

                return ValidationResult.Success;
            }

            return new ValidationResult("Missing runtime context to create branch!");
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Checkout branch from stash...";

            var log = _repo.CreateLog($"Checkout Branch '{_branchName}'");
            Use(log);

            var succ = await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .CheckoutBranchAsync(Target.Name, _branchName);

            if (succ)
            {
                _repo.MarkWorkingCopyDirtyManually();
                _repo.MarkStashesDirtyManually();
            }

            log.Complete();
            return true;
        }

        private readonly Repository _repo;
        private string _branchName = string.Empty;
    }
}
