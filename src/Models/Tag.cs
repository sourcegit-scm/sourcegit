using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public enum TagSortMode
    {
        CreatorDate = 0,
        Name,
    }

    public class Tag : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAnnotated { get; set; }
        public string SHA { get; set; } = string.Empty;
        public ulong CreatorDate { get; set; }
        public string Message { get; set; } = string.Empty;

        public FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        private FilterMode _filterMode = FilterMode.None;
    }
}
