using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class PushToNewBranch : ObservableValidator
    {
        public string Remote
        {
            get;
        }

        [Required(ErrorMessage = "Branch name is required!")]
        [CustomValidation(typeof(PushToNewBranch), nameof(ValidateBranchName))]
        public string BranchName
        {
            get => _branchName;
            set => SetProperty(ref _branchName, value, true);
        }

        public PushToNewBranch(string remote)
        {
            Remote = remote;
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (!Models.RefName.IsValidBranchName(name))
                return new ValidationResult("Bad branch name format!");

            return ValidationResult.Success;
        }

        [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode")]
        public bool Check()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        private string _branchName = string.Empty;
    }
}
