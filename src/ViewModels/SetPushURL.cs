using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class SetPushURL : Popup
    {
        public Models.Remote Remote
        {
            get => _remote;
        }

        [CustomValidation(typeof(SetPushURL), nameof(ValidatePushURL))]
        public string PushURL
        {
            get => _pushURL;
            set => SetProperty(ref _pushURL, value, true);
        }

        public SetPushURL(Repository repo, Models.Remote remote)
        {
            _repo = repo;
            _remote = remote;
            _pushURL = string.IsNullOrEmpty(remote.PushURL) ? remote.URL : remote.PushURL;
        }

        public static ValidationResult ValidatePushURL(string url, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is SetPushURL set)
            {
                if (!string.IsNullOrEmpty(url) && !Models.Remote.IsValidURL(url))
                    return new ValidationResult("Bad remote URL format!!!");
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            if (_remote.PushURL == _pushURL)
                return true;

            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Set PushURL for '{_remote.Name}' ...";

            var succ = false;
            if (string.IsNullOrEmpty(_pushURL))
            {
                succ = await new Commands.Remote(_repo.FullPath)
                    .SetURLAsync(_remote.Name, _remote.PushURL, true, true);
            }
            else if (_pushURL.Equals(_remote.URL, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(_remote.PushURL))
                    succ = await new Commands.Remote(_repo.FullPath).SetURLAsync(_remote.Name, _remote.PushURL, true, true);
                else
                    succ = true;

                _pushURL = null;
            }
            else
            {
                succ = await new Commands.Remote(_repo.FullPath)
                    .SetURLAsync(_remote.Name, _pushURL, false, true);
            }

            if (succ)
                _remote.PushURL = _pushURL;

            return succ;
        }

        private readonly Repository _repo;
        private readonly Models.Remote _remote;
        private string _pushURL;
    }
}
