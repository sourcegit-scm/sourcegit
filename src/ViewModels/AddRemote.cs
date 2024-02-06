using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class AddRemote : Popup {
        [Required(ErrorMessage = "Remote name is required!!!")]
        [RegularExpression(@"^[\w\-\.]+$", ErrorMessage = "Bad remote name format!!!")]
        [CustomValidation(typeof(AddRemote), nameof(ValidateRemoteName))]
        public string Name {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        [Required(ErrorMessage = "Remote URL is required!!!")]
        [CustomValidation(typeof(AddRemote), nameof(ValidateRemoteURL))]
        public string Url {
            get => _url;
            set {
                if (SetProperty(ref _url, value, true)) UseSSH = Models.Remote.IsSSH(value);
            }
        }

        public bool UseSSH {
            get => _useSSH;
            set => SetProperty(ref _useSSH, value);
        }

        public string SSHKey {
            get;
            set;
        }

        public AddRemote(Repository repo) {
            _repo = repo;
            View = new Views.AddRemote() { DataContext = this };
        }

        public static ValidationResult ValidateRemoteName(string name, ValidationContext ctx) {
            if (ctx.ObjectInstance is AddRemote add) {
                var exists = add._repo.Remotes.Find(x => x.Name == name);
                if (exists != null) return new ValidationResult("A remote with given name already exists!!!");
            }

            return ValidationResult.Success;
        }

        public static ValidationResult ValidateRemoteURL(string url, ValidationContext ctx) {
            if (ctx.ObjectInstance is AddRemote add) {
                if (!Models.Remote.IsValidURL(url)) return new ValidationResult("Bad remote URL format!!!");

                var exists = add._repo.Remotes.Find(x => x.URL == url);
                if (exists != null) return new ValidationResult("A remote with the same url already exists!!!");
            }

            return ValidationResult.Success;
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() => {
                SetProgressDescription("Adding remote ...");
                var succ = new Commands.Remote(_repo.FullPath).Add(_name, _url);
                if (succ) {
                    SetProgressDescription("Fetching from added remote ...");
                    new Commands.Fetch(_repo.FullPath, _name, true, SetProgressDescription).Exec();

                    if (_useSSH) {
                        SetProgressDescription("Post processing ...");
                        new Commands.Config(_repo.FullPath).Set($"remote.{_name}.sshkey", SSHKey);
                    }
                }
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
        private string _name = string.Empty;
        private string _url = string.Empty;
        private bool _useSSH = false;
    }
}
