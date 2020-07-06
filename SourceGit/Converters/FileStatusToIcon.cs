using System;
using System.Globalization;
using System.Windows.Data;

namespace SourceGit.Converters {

    /// <summary>
    ///     Convert file status to icon.
    /// </summary>
    public class FileStatusToIcon : IValueConverter {

        /// <summary>
        ///     Is only test local changes.
        /// </summary>
        public bool OnlyWorkTree { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var change = value as Git.Change;
            if (change == null) return "";

            var status = Git.Change.Status.None;
            if (OnlyWorkTree) {
                if (change.IsConflit) return "X";
                status = change.WorkTree;
            } else {
                status = change.Index;
            }

            switch (status) {
            case Git.Change.Status.Modified: return "M";
            case Git.Change.Status.Added: return "A";
            case Git.Change.Status.Deleted: return "D";
            case Git.Change.Status.Renamed: return "R";
            case Git.Change.Status.Copied: return "C";
            case Git.Change.Status.Unmerged: return "U";
            case Git.Change.Status.Untracked: return "?";
            default: return "?";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
