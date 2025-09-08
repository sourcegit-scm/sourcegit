using System;
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

        public string RelativePath
        {
            get => _relativePath;
            set => SetProperty(ref _relativePath, value);
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

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Adding submodule...";

            var log = _repo.CreateLog("Add Submodule");
            Use(log);

            var relativePath = _relativePath;
            if (string.IsNullOrEmpty(relativePath))
            {
                if (_url.EndsWith("/.git", StringComparison.Ordinal))
                    relativePath = Path.GetFileName(Path.GetDirectoryName(_url));
                else if (_url.EndsWith(".git", StringComparison.Ordinal))
                    relativePath = Path.GetFileNameWithoutExtension(_url);
                else
                    relativePath = Path.GetFileName(_url);
            }

            var succ = await new Commands.Submodule(_repo.FullPath)
                .Use(log)
                .AddAsync(_url, relativePath, Recursive);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
        private string _url = string.Empty;
        private string _relativePath = string.Empty;
    }
}
