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

            var isCurrent = Target.IsCurrent;
            var oldName = Target.FullName;

            var succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                .Use(log)
                .RenameAsync(_name);

            if (succ)
            {
                foreach (var filter in _repo.HistoryFilterCollection.Filters)
                {
                    if (filter.Type == Models.FilterType.LocalBranch &&
                        filter.Pattern.Equals(oldName, StringComparison.Ordinal))
                    {
                        filter.Pattern = $"refs/heads/{_name}";
                        break;
                    }
                }
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();

            if (isCurrent)
            {
                ProgressDescription = "Waiting for branch updated...";
                await Task.Delay(400);
            }

            return succ;
        }

        private readonly Repository _repo;
        private string _name;
    }
}
