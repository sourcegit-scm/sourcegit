using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace SourceGit.Converters {

    public class PathToFileName : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Path.GetFileName(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class PathToFolderName : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Path.GetDirectoryName(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
