using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class EditRemote : Popup
    {
        [Required(ErrorMessage = "Remote name is required!!!")]
        [RegularExpression(@"^[\w\-\.]+$", ErrorMessage = "Bad remote name format!!!")]
        [CustomValidation(typeof(EditRemote), nameof(ValidateRemoteName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        [Required(ErrorMessage = "Remote URL is required!!!")]
        [CustomValidation(typeof(EditRemote), nameof(ValidateRemoteURL))]
        public string Url
        {
            get => _url;
            set
            {
                if (SetProperty(ref _url, value, true))
                    UseSSH = Models.Remote.IsSSH(value);
            }
        }

        public bool UseSSH
        {
            get => _useSSH;
            set
            {
                if (SetProperty(ref _useSSH, value))
                    ValidateProperty(_sshkey, nameof(SSHKey));
            }
        }

        [CustomValidation(typeof(EditRemote), nameof(ValidateSSHKey))]
        public string SSHKey
        {
            get => _sshkey;
            set => SetProperty(ref _sshkey, value, true);
        }

        public EditRemote(Repository repo, Models.Remote remote)
        {
            _repo = repo;
            _remote = remote;
            _name = remote.Name;
            _url = remote.URL;
            _useSSH = Models.Remote.IsSSH(remote.URL);

            if (_useSSH)
                _sshkey = new Commands.Config(repo.FullPath).Get($"remote.{remote.Name}.sshkey");
        }

        public static ValidationResult ValidateRemoteName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is EditRemote edit)
            {
                foreach (var remote in edit._repo.Remotes)
                {
                    if (remote != edit._remote && name == remote.Name)
                        return new ValidationResult("A remote with given name already exists!!!");
                }
            }

            return ValidationResult.Success;
        }

        public static ValidationResult ValidateRemoteURL(string url, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is EditRemote edit)
            {
                if (!Models.Remote.IsValidURL(url))
                    return new ValidationResult("Bad remote URL format!!!");

                foreach (var remote in edit._repo.Remotes)
                {
                    if (remote != edit._remote && url == remote.URL)
                        return new ValidationResult("A remote with the same url already exists!!!");
                }
            }

            return ValidationResult.Success;
        }

        public static ValidationResult ValidateSSHKey(string sshkey, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is EditRemote { _useSSH: true } && !string.IsNullOrEmpty(sshkey))
            {
                if (!File.Exists(sshkey))
                    return new ValidationResult("Given SSH private key can NOT be found!");
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Editing remote '{_remote.Name}' ...";

            if (_remote.Name != _name)
            {
                var succ = await new Commands.Remote(_repo.FullPath).RenameAsync(_remote.Name, _name);
                if (succ)
                    _remote.Name = _name;
            }

            if (_remote.URL != _url)
            {
                var succ = await new Commands.Remote(_repo.FullPath).SetURLAsync(_name, _url, false);
                if (succ)
                    _remote.URL = _url;
            }

            var pushURL = await new Commands.Remote(_repo.FullPath).GetURLAsync(_name, true);
            if (pushURL != _url)
                await new Commands.Remote(_repo.FullPath).SetURLAsync(_name, _url, true);

            await new Commands.Config(_repo.FullPath).SetAsync($"remote.{_name}.sshkey", _useSSH ? SSHKey : null);

            _repo.SetWatcherEnabled(true);
            return true;
        }

        private readonly Repository _repo = null;
        private readonly Models.Remote _remote = null;
        private string _name = null;
        private string _url = null;
        private bool _useSSH = false;
        private string _sshkey = string.Empty;
    }
}
