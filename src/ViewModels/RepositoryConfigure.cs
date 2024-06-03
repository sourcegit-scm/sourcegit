using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class RepositoryConfigure : Popup
    {
        public string UserName
        {
            get;
            set;
        }

        public string UserEmail
        {
            get;
            set;
        }

        public Models.GPGFormat GPGFormat
        {
            get;
            set;
        }

        public bool GPGCommitSigningEnabled
        {
            get;
            set;
        }

        public bool GPGTagSigningEnabled
        {
            get;
            set;
        }

        public string GPGUserSigningKey
        {
            get;
            set;
        }

        public string HttpProxy
        {
            get;
            set;
        }

        public RepositoryConfigure(Repository repo)
        {
            _repo = repo;

            _cached = new Commands.Config(repo.FullPath).ListAll();
            if (_cached.TryGetValue("user.name", out var name))
                UserName = name;
            if (_cached.TryGetValue("user.email", out var email))
                UserEmail = email;
            if (_cached.TryGetValue("commit.gpgsign", out var gpgCommitSign))
                GPGCommitSigningEnabled = gpgCommitSign == "true";
            if (_cached.TryGetValue("tag.gpgSign", out var gpgTagSign))
                GPGTagSigningEnabled = gpgTagSign == "true";
            if (_cached.TryGetValue("gpg.format", out var gpgFormat))
                GPGFormat = Models.GPGFormat.Supported.Find(x => x.Value == gpgFormat);
            else
                GPGFormat = Models.GPGFormat.OPENPGP;
            if (_cached.TryGetValue("user.signingkey", out var signingKey))
                GPGUserSigningKey = signingKey;
            if (_cached.TryGetValue("http.proxy", out var proxy))
                HttpProxy = proxy;

            View = new Views.RepositoryConfigure() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            SetIfChanged("user.name", UserName);
            SetIfChanged("user.email", UserEmail);
            SetIfChanged("commit.gpgsign", GPGCommitSigningEnabled ? "true" : "false");
            SetIfChanged("tag.gpgSign", GPGTagSigningEnabled ? "true" : "false");
            SetIfChanged("gpg.format", GPGFormat?.Value, Models.GPGFormat.OPENPGP.Value);
            SetIfChanged("user.signingkey", GPGUserSigningKey);
            SetIfChanged("http.proxy", HttpProxy);
            return null;
        }

        private void SetIfChanged(string key, string value, string defaultValue = null)
        {
            bool changed = false;
            if (_cached.TryGetValue(key, out var old))
            {
                changed = old != value;
            }
            else if (!string.IsNullOrEmpty(value) && value != defaultValue)
            {
                changed = true;
            }

            if (changed)
            {
                new Commands.Config(_repo.FullPath).Set(key, value);
            }
        }

        private readonly Repository _repo = null;
        private readonly Dictionary<string, string> _cached = null;
    }
}
