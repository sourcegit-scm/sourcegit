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
        [RegularExpression(@"^[\w\-/\.#]+$", ErrorMessage = "Bad branch name format!")]
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
            View = new Views.RenameBranch() { DataContext = this };
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is RenameBranch rename)
            {
                foreach (var b in rename._repo.Branches)
                {
                    if (b.IsLocal && b != rename.Target && b.Name == name)
                    {
                        return new ValidationResult("A branch with same name already exists!!!");
                    }
                }
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            if (_name == Target.Name)
                return null;

            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Rename '{Target.Name}'";

            return Task.Run(() =>
            {
                var oldName = Target.FullName;
                var succ = Commands.Branch.Rename(_repo.FullPath, Target.Name, _name);
                CallUIThread(() =>
                {
                    if (succ && _repo.Settings.Filters.Contains(oldName))
                    {
                        _repo.Settings.Filters.Remove(oldName);
                        _repo.Settings.Filters.Add($"refs/heads/{_name}");
                    }
                    
                    _repo.MarkBranchesDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });
                return succ;
            });
        }

        private readonly Repository _repo;
        private string _name;
    }
}
