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
                var fixedName = rename.FixName(name);
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

        public override Task<bool> Sure()
        {
            var fixedName = FixName(_name);
            if (fixedName == Target.Name)
                return null;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Rename '{Target.Name}'";

            var log = _repo.CreateLog($"Rename Branch '{Target.Name}'");
            Use(log);

            return Task.Run(() =>
            {
                var oldName = Target.FullName;
                var succ = Commands.Branch.Rename(_repo.FullPath, Target.Name, fixedName, log);
                log.Complete();

                CallUIThread(() =>
                {
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

                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });
                return succ;
            });
        }

        private string FixName(string name)
        {
            if (!name.Contains(' '))
                return name;

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join("-", parts);
        }

        private readonly Repository _repo;
        private string _name;
    }
}
