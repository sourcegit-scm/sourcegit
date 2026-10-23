using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    /// <summary>
    ///     A place where git runs besides this PC: a WSL distro. This PC itself is implicit and not stored.
    ///     Repositories belong to a server by their path (//wsl.localhost/&lt;Distro&gt;/...), not by a stored reference.
    /// </summary>
    public class GitServer : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Distro
        {
            get => _distro;
            set => SetProperty(ref _distro, value);
        }

        // Empty: Git Credential Manager of this PC if installed, else the distro's own git config.
        public string CredentialHelper
        {
            get;
            set;
        } = string.Empty;

        [JsonIgnore]
        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }

        private string _name = string.Empty;
        private string _distro = string.Empty;
        private string _testResult = string.Empty;
    }
}
