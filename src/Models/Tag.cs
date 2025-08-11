using System;
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
        public bool IsAnnotated { get; set; } = false;
        public string SHA { get; set; } = string.Empty;
        public User Creator { get; set; } = null;
        public ulong CreatorDate { get; set; } = 0;
        public string Message { get; set; } = string.Empty;

        public string CreatorDateStr
        {
            get => DateTime.UnixEpoch.AddSeconds(CreatorDate).ToLocalTime().ToString(DateTimeFormat.Active.DateTime);
        }

        public FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        private FilterMode _filterMode = FilterMode.None;
    }
}
