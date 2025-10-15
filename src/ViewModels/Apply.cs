using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Apply : Popup
    {
        [Required(ErrorMessage = "Patch file is required!!!")]
        [CustomValidation(typeof(Apply), nameof(ValidatePatchFile))]
        public string PatchFile
        {
            get => _patchFile;
            set => SetProperty(ref _patchFile, value, true);
        }

        public bool IgnoreWhiteSpace
        {
            get => _ignoreWhiteSpace;
            set => SetProperty(ref _ignoreWhiteSpace, value);
        }

        public Models.ApplyWhiteSpaceMode SelectedWhiteSpaceMode
        {
            get;
            set;
        }

        public Apply(Repository repo)
        {
            _repo = repo;

            SelectedWhiteSpaceMode = Models.ApplyWhiteSpaceMode.Supported[0];
        }

        public static ValidationResult ValidatePatchFile(string file, ValidationContext _)
        {
            if (File.Exists(file))
                return ValidationResult.Success;

            return new ValidationResult($"File '{file}' can NOT be found!!!");
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Apply patch...";

            var log = _repo.CreateLog("Apply Patch");
            Use(log);

            var succ = await new Commands.Apply(_repo.FullPath, _patchFile, _ignoreWhiteSpace, SelectedWhiteSpaceMode.Arg, null)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
        private string _patchFile = string.Empty;
        private bool _ignoreWhiteSpace = true;
    }
}
