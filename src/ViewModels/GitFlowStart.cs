using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class GitFlowStart : Popup
    {
        public Models.GitFlowBranchType Type
        {
            get;
            private set;
        }

        public string Prefix
        {
            get;
            private set;
        }

        [Required(ErrorMessage = "Name is required!!!")]
        [RegularExpression(@"^[\w\-/\.#]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(GitFlowStart), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public GitFlowStart(Repository repo, Models.GitFlowBranchType type)
        {
            _repo = repo;

            Type = type;
            Prefix = _repo.GitFlow.GetPrefix(type);
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is GitFlowStart starter)
            {
                var check = $"{starter.Prefix}{name}";
                foreach (var b in starter._repo.Branches)
                {
                    if (b.FriendlyName == check)
                        return new ValidationResult("A branch with same name already exists!");
                }
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Git Flow - Start {Prefix}{_name} ...";

            var log = _repo.CreateLog("GitFlow - Start");
            Use(log);

            var succ = await Commands.GitFlow.StartAsync(_repo.FullPath, Type, _name, log);
            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private string _name = null;
    }
}
