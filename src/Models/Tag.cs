using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public enum TagSortMode
    {
        CreatorDate = 0,
        NameInAscending,
        NameInDescending,
    }

    public enum TagType
    {
        Lightweight = 0,
        Annotated,
    }

    public class Tag : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public TagType Type { get; set; } = TagType.Lightweight;
        public string SHA { get; set; } = string.Empty;
        public ulong CreatorDate { get; set; } = 0;
        public string Message { get; set; } = string.Empty;

        public FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        private FilterMode _filterMode = FilterMode.None;

        public bool IsLightweight => (Type == TagType.Lightweight);
    }
}
