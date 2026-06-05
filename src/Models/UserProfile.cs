using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    

    public record UserProfileTargetFile(string File, Commit Revision);


    public class UserProfile : ObservableObject
    {
        public string ProfileName
        {
            get => _profileName;
            set => SetProperty(ref _profileName, value);
        }

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }


        private string _profileName = string.Empty;
        private string _userName = string.Empty;
        private string _email = string.Empty;
        private string _key = string.Empty;
    }
}
