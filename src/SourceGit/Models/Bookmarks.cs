using System.Collections.Generic;

namespace SourceGit.Models
{
    public static class Bookmarks
    {
        public static readonly Avalonia.Media.IBrush[] Brushes = [
            Avalonia.Media.Brushes.Transparent,
            Avalonia.Media.Brushes.Red,
            Avalonia.Media.Brushes.Orange,
            Avalonia.Media.Brushes.Gold,
            Avalonia.Media.Brushes.ForestGreen,
            Avalonia.Media.Brushes.DarkCyan,
            Avalonia.Media.Brushes.DeepSkyBlue,
            Avalonia.Media.Brushes.Purple,
        ];

        public static readonly List<int> Supported = new List<int>();

        static Bookmarks()
        {
            for (int i = 0; i < Brushes.Length; i++)
                Supported.Add(i);
        }
    }
}
