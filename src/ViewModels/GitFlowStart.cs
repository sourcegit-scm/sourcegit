using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowStart : Popup
    {
        [Required(ErrorMessage = "Name is required!!!")]
        [RegularExpression(@"^[\w\-/\.]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(GitFlowStart), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public string Prefix
        {
            get => _prefix;
        }

        public bool IsFeature => _type == Models.GitFlowBranchType.Feature;
        public bool IsRelease => _type == Models.GitFlowBranchType.Release;
        public bool IsHotfix => _type == Models.GitFlowBranchType.Hotfix;

        public GitFlowStart(Repository repo, Models.GitFlowBranchType type)
        {
            _repo = repo;
            _type = type;

            switch (type)
            {
                case Models.GitFlowBranchType.Feature:
                    _prefix = repo.GitFlow.Feature;
                    break;
                case Models.GitFlowBranchType.Release:
                    _prefix = repo.GitFlow.Release;
                    break;
                default:
                    _prefix = repo.GitFlow.Hotfix;
                    break;
            }

            View = new Views.GitFlowStart() { DataContext = this };
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is GitFlowStart starter)
            {
                var check = $"{starter._prefix}{name}";
                foreach (var b in starter._repo.Branches)
                {
                    var test = b.IsLocal ? b.Name : $"{b.Remote}/{b.Name}";
                    if (test == check)
                        return new ValidationResult("A branch with same name already exists!");
                }
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() =>
            {
                SetProgressDescription($"Git Flow - starting {_prefix}{_name} ...");
                var succ = new Commands.GitFlow(_repo.FullPath).Start(_type, _name);
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private readonly Models.GitFlowBranchType _type = Models.GitFlowBranchType.Feature;
        private readonly string _prefix = string.Empty;
        private string _name = null;
    }
}
