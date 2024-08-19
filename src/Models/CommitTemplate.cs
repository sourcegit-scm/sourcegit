using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public class CommitTemplate : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        private string _name = string.Empty;
        private string _content = string.Empty;
    }
}
