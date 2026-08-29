using System;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public enum LauncherTabLayout
    {
        Horizontal = 0,
        Vertical = 1,
    }

    public class LauncherTabSettings : ObservableObject
    {
        public LauncherTabLayout Layout
        {
            get => _layout;
            set
            {
                if (SetProperty(ref _layout, value))
                    OnPropertyChanged(nameof(IsVertical));
            }
        }

        public double VerticalWidth
        {
            get => _verticalWidth;
            set => SetProperty(ref _verticalWidth, Math.Clamp(value, 160, 420));
        }

        public bool IsVertical => _layout == LauncherTabLayout.Vertical;

        private LauncherTabLayout _layout = LauncherTabLayout.Horizontal;
        private double _verticalWidth = 220;
    }
}
