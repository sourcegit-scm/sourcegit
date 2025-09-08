namespace SourceGit.Models
{
    public static class Bookmarks
    {
        public static readonly Avalonia.Media.IBrush[] Brushes = [
            null,
            Avalonia.Media.Brushes.Red,
            Avalonia.Media.Brushes.Orange,
            Avalonia.Media.Brushes.Gold,
            Avalonia.Media.Brushes.ForestGreen,
            Avalonia.Media.Brushes.DarkCyan,
            Avalonia.Media.Brushes.DeepSkyBlue,
            Avalonia.Media.Brushes.Purple,
        ];

        public static Avalonia.Media.IBrush Get(int i)
        {
            return (i >= 0 && i < Brushes.Length) ? Brushes[i] : null;
        }
    }
}
