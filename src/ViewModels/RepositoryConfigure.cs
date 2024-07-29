using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RepositoryConfigure : ObservableObject
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
            get => _httpProxy;
            set => SetProperty(ref _httpProxy, value);
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
            if (_cached.TryGetValue("tag.gpgsign", out var gpgTagSign))
                GPGTagSigningEnabled = gpgTagSign == "true";
            if (_cached.TryGetValue("user.signingkey", out var signingKey))
                GPGUserSigningKey = signingKey;
            if (_cached.TryGetValue("http.proxy", out var proxy))
                HttpProxy = proxy;
        }

        public void ClearHttpProxy()
        {
            HttpProxy = string.Empty;
        }

        public void Save()
        {
            SetIfChanged("user.name", UserName);
            SetIfChanged("user.email", UserEmail);
            SetIfChanged("commit.gpgsign", GPGCommitSigningEnabled ? "true" : "false");
            SetIfChanged("tag.gpgsign", GPGTagSigningEnabled ? "true" : "false");
            SetIfChanged("user.signingkey", GPGUserSigningKey);
            SetIfChanged("http.proxy", HttpProxy);
        }

        private void SetIfChanged(string key, string value)
        {
            bool changed = false;
            if (_cached.TryGetValue(key, out var old))
            {
                changed = old != value;
            }
            else if (!string.IsNullOrEmpty(value))
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
        private string _httpProxy;
    }
}
