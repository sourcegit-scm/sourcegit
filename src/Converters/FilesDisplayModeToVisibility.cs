using System;
using System.Globalization;
using System.Windows;
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

    public class FilesDisplayModeToList : IValueConverter {

        public bool TreatGridAsList { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var mode = (Git.Preference.FilesDisplayMode)value;
            if (mode == Git.Preference.FilesDisplayMode.Tree) return Visibility.Collapsed;
            if (mode == Git.Preference.FilesDisplayMode.List) return Visibility.Visible;
            if (TreatGridAsList) return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class FilesDisplayModeToGrid : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (Git.Preference.FilesDisplayMode)value == Git.Preference.FilesDisplayMode.Grid ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class FilesDisplayModeToTree : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (Git.Preference.FilesDisplayMode)value == Git.Preference.FilesDisplayMode.Tree ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
