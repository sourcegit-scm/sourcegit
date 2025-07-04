using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public partial class RenameBranch : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        [Required(ErrorMessage = "Branch name is required!!!")]
        [RegularExpression(@"^[\w \-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
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
                var fixedName = FixName(name);
                foreach (var b in rename._repo.Branches)
                {
                    if (b.IsLocal && b != rename.Target && b.Name == fixedName)
                    {
                        return new ValidationResult("A branch with same name already exists!!!");
                    }
                }
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            var fixedName = FixName(_name);
            if (fixedName == Target.Name)
                return true;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Rename '{Target.Name}'";

            var log = _repo.CreateLog($"Rename Branch '{Target.Name}'");
            Use(log);

            var isCurrent = Target.IsCurrent;
            var oldName = Target.FullName;
            var succ = await Commands.Branch
                .RenameAsync(_repo.FullPath, Target.Name, fixedName, log);

            if (succ)
            {
                foreach (var filter in _repo.Settings.HistoriesFilters)
                {
                    if (filter.Type == Models.FilterType.LocalBranch &&
                        filter.Pattern.Equals(oldName, StringComparison.Ordinal))
                    {
                        filter.Pattern = $"refs/heads/{fixedName}";
                        break;
                    }
                }
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            _repo.SetWatcherEnabled(true);

            if (isCurrent)
            {
                ProgressDescription = "Waiting for branch updated...";
                await Task.Delay(400);
            }

            return succ;
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex REG_FIX_NAME();

        private static string FixName(string name)
        {
            return REG_FIX_NAME().Replace(name, "-");
        }

        private readonly Repository _repo;
        private string _name;
    }
}
