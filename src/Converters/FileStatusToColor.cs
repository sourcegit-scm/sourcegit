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

            switch (status) {
            case Git.Change.Status.Modified: return new LinearGradientBrush(Colors.Orange, Color.FromRgb(255, 213, 134), 90);
            case Git.Change.Status.Added: return new LinearGradientBrush(Colors.LimeGreen, Color.FromRgb(124, 241, 124), 90);
            case Git.Change.Status.Deleted: return new LinearGradientBrush(Colors.Tomato, Color.FromRgb(252, 165, 150), 90);
            case Git.Change.Status.Renamed: return new LinearGradientBrush(Colors.Orchid, Color.FromRgb(248, 161, 245), 90);
            case Git.Change.Status.Copied: return new LinearGradientBrush(Colors.Orange, Color.FromRgb(255, 213, 134), 90);
            case Git.Change.Status.Unmerged: return new LinearGradientBrush(Colors.Orange, Color.FromRgb(255, 213, 134), 90);
            case Git.Change.Status.Untracked: return new LinearGradientBrush(Colors.LimeGreen, Color.FromRgb(124, 241, 124), 90);
            default: return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
