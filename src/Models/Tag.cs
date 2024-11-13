﻿using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public class Tag : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string SHA { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        private FilterMode _filterMode = FilterMode.None;
    }
}
