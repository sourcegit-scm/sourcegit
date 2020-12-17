using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SourceGit.Converters {

    public class FilesDisplayModeToList : IValueConverter {

        public bool TreatGridAsList { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var mode = (Preference.FilesDisplayMode)value;
            if (mode == Preference.FilesDisplayMode.Tree) return Visibility.Collapsed;
            if (mode == Preference.FilesDisplayMode.List) return Visibility.Visible;
            if (TreatGridAsList) return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class FilesDisplayModeToGrid : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (Preference.FilesDisplayMode)value == Preference.FilesDisplayMode.Grid ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class FilesDisplayModeToTree : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (Preference.FilesDisplayMode)value == Preference.FilesDisplayMode.Tree ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
