using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SourceGit.Converters {

    public class FilesDisplayModeToIcon : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var mode = (Git.Preference.FilesDisplayMode)value;
            switch (mode) {
            case Git.Preference.FilesDisplayMode.Grid:
                return App.Current.FindResource("Icon.Grid") as Geometry;
            case Git.Preference.FilesDisplayMode.List:
                return App.Current.FindResource("Icon.List") as Geometry;
            default:
                return App.Current.FindResource("Icon.Tree") as Geometry;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
