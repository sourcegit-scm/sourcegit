using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SourceGit.Converters {

    /// <summary>
    ///     Convert file status to brush
    /// </summary>
    public class FileStatusToColor : IValueConverter {

        /// <summary>
        ///     Is only test local changes.
        /// </summary>
        public bool OnlyWorkTree { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var change = value as Git.Change;
            if (change == null) return Brushes.Transparent;

            var status = Git.Change.Status.None;
            if (OnlyWorkTree) {
                if (change.IsConflit) return Brushes.Yellow;
                status = change.WorkTree;
            } else {
                status = change.Index;
            }

            if (App.Preference.UIUseLightTheme) {
                switch (status) {
                case Git.Change.Status.Modified: return Brushes.Goldenrod;
                case Git.Change.Status.Added: return Brushes.Green;
                case Git.Change.Status.Deleted: return Brushes.Red;
                case Git.Change.Status.Renamed: return Brushes.Magenta;
                case Git.Change.Status.Copied: return Brushes.Goldenrod;
                case Git.Change.Status.Unmerged: return Brushes.Goldenrod;
                case Git.Change.Status.Untracked: return Brushes.Green;
                default: return Brushes.Transparent;
                }
            } else {
                switch (status) {
                case Git.Change.Status.Modified: return Brushes.DarkGoldenrod;
                case Git.Change.Status.Added: return Brushes.DarkGreen;
                case Git.Change.Status.Deleted: return Brushes.DarkRed;
                case Git.Change.Status.Renamed: return Brushes.DarkMagenta;
                case Git.Change.Status.Copied: return Brushes.DarkGoldenrod;
                case Git.Change.Status.Unmerged: return Brushes.DarkGoldenrod;
                case Git.Change.Status.Untracked: return Brushes.DarkGreen;
                default: return Brushes.Transparent;
                }
            }            
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
