using System;

namespace SourceGit.Models
{
    public enum LauncherTabLayout
    {
        Horizontal = 0,
        Vertical = 1,
    }

    public class LauncherTabSettings
    {
        public LauncherTabLayout Layout
        {
            get => _layout;
            set => _layout = value;
        }

        public double VerticalWidth
        {
            get => _verticalWidth;
            set => _verticalWidth = Math.Clamp(value, 160, 420);
        }

        private LauncherTabLayout _layout = LauncherTabLayout.Horizontal;
        private double _verticalWidth = 220;
    }
}
