using System;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.Models
{
    public class GitAccount : ObservableObject
    {
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string GitUserName
        {
            get => _gitUserName;
            set => SetProperty(ref _gitUserName, value);
        }

        public string GitEmail
        {
            get => _gitEmail;
            set => SetProperty(ref _gitEmail, value);
        }

        public string GitHubUserName
        {
            get => _gitHubUserName;
            set => SetProperty(ref _gitHubUserName, value);
        }

        public bool MatchesIdentity(string userName, string email)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email))
                return false;

            return string.Equals(GitUserName, userName, StringComparison.Ordinal) &&
                string.Equals(GitEmail, email, StringComparison.OrdinalIgnoreCase);
        }

        private string _id = Guid.NewGuid().ToString("N");
        private string _name = string.Empty;
        private string _gitUserName = string.Empty;
        private string _gitEmail = string.Empty;
        private string _gitHubUserName = string.Empty;
    }
}
