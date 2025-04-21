using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class AddSubmodule : Popup
    {
        [Required(ErrorMessage = "Url is required!!!")]
        [CustomValidation(typeof(AddSubmodule), nameof(ValidateURL))]
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value, true);
        }

        [Required(ErrorMessage = "Reletive path is required!!!")]
        [CustomValidation(typeof(AddSubmodule), nameof(ValidateRelativePath))]
        public string RelativePath
        {
            get => _relativePath;
            set => SetProperty(ref _relativePath, value, true);
        }

        public bool Recursive
        {
            get;
            set;
        }

        public AddSubmodule(Repository repo)
        {
            _repo = repo;
        }

        public static ValidationResult ValidateURL(string url, ValidationContext ctx)
        {
            if (!Models.Remote.IsValidURL(url))
                return new ValidationResult("Invalid repository URL format");
            return ValidationResult.Success;
        }

        public static ValidationResult ValidateRelativePath(string path, ValidationContext ctx)
        {
            if (Path.Exists(path))
            {
                return new ValidationResult("Give path is exists already!");
            }

            if (Path.IsPathRooted(path))
            {
                return new ValidationResult("Path must be relative to this repository!");
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Adding submodule...";

            var log = _repo.CreateLog("Add Submodule");
            Use(log);

            return Task.Run(() =>
            {
                var succ = new Commands.Submodule(_repo.FullPath).Use(log).Add(_url, _relativePath, Recursive);
                log.Complete();

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private string _url = string.Empty;
        private string _relativePath = string.Empty;
    }
}
