using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class AddRemote : Popup
    {
        [Required(ErrorMessage = "Remote name is required!!!")]
        [RegularExpression(@"^[\w\-\.]+$", ErrorMessage = "Bad remote name format!!!")]
        [CustomValidation(typeof(AddRemote), nameof(ValidateRemoteName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        [Required(ErrorMessage = "Remote URL is required!!!")]
        [CustomValidation(typeof(AddRemote), nameof(ValidateRemoteURL))]
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

        [CustomValidation(typeof(AddRemote), nameof(ValidateSSHKey))]
        public string SSHKey
        {
            get => _sshkey;
            set => SetProperty(ref _sshkey, value, true);
        }

        public AddRemote(Repository repo)
        {
            _repo = repo;
        }

        public static ValidationResult ValidateRemoteName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is AddRemote add)
            {
                var exists = add._repo.Remotes.Find(x => x.Name == name);
                if (exists != null)
                    return new ValidationResult("A remote with given name already exists!!!");
            }

            return ValidationResult.Success;
        }

        public static ValidationResult ValidateRemoteURL(string url, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is AddRemote add)
            {
                if (!Models.Remote.IsValidURL(url))
                    return new ValidationResult("Bad remote URL format!!!");

                var exists = add._repo.Remotes.Find(x => x.URL == url);
                if (exists != null)
                    return new ValidationResult("A remote with the same url already exists!!!");
            }

            return ValidationResult.Success;
        }

        public static ValidationResult ValidateSSHKey(string sshkey, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is AddRemote { _useSSH: true } && !string.IsNullOrEmpty(sshkey))
            {
                if (!File.Exists(sshkey))
                    return new ValidationResult("Given SSH private key can NOT be found!");
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Adding remote ...";

            var log = _repo.CreateLog("Add Remote");
            Use(log);

            var succ = await new Commands.Remote(_repo.FullPath)
                .Use(log)
                .AddAsync(_name, _url);

            if (succ)
            {
                await new Commands.Config(_repo.FullPath)
                    .Use(log)
                    .SetAsync($"remote.{_name}.sshkey", _useSSH ? SSHKey : null);

                await new Commands.Fetch(_repo.FullPath, _name, false, false)
                    .Use(log)
                    .RunAsync();
            }

            log.Complete();

            _repo.MarkFetched();
            _repo.MarkBranchesDirtyManually();
            return succ;
        }

        private readonly Repository _repo = null;
        private string _name = string.Empty;
        private string _url = string.Empty;
        private bool _useSSH = false;
        private string _sshkey = string.Empty;
    }
}
