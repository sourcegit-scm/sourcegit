using System;
using System.Globalization;
using System.Windows.Data;

namespace SourceGit.Converters {

    /// <summary>
    ///     Inverse bool converter.
    /// </summary>
    public class InverseBool : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return !((bool)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
