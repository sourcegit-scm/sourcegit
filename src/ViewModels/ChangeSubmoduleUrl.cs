using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ChangeSubmoduleUrl : Popup
    {
        public Models.Submodule Submodule
        {
            get;
        }

        [Required(ErrorMessage = "Url is required!!!")]
        [CustomValidation(typeof(ChangeSubmoduleUrl), nameof(ValidateUrl))]
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value, true);
        }

        public ChangeSubmoduleUrl(Repository repo, Models.Submodule submodule)
        {
            _repo = repo;
            _url = submodule.URL;
            Submodule = submodule;
        }

        public static ValidationResult ValidateUrl(string url, ValidationContext ctx)
        {
            if (!Models.Remote.IsValidURL(url))
                return new ValidationResult("Invalid repository URL format");

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            if (_url.Equals(Submodule.URL, StringComparison.Ordinal))
                return true;

            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Change submodule's url...";

            var log = _repo.CreateLog("Change Submodule's URL");
            Use(log);

            var succ = await new Commands.Submodule(_repo.FullPath)
                .Use(log)
                .SetURLAsync(Submodule.Path, _url);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private string _url;
    }
}
