using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RecentRepo : ObservableObject
    {
        public string Path { get; set; }
        public string Name { get; set; }

        public RecentRepo(string path, string name)
        {
            Path = path;
            Name = name;
        }
    }
}
