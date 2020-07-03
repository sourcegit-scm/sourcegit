using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SourceGit.Converters {

    /// <summary>
    ///     Same as BoolToVisibilityConverter.
    /// </summary>
    public class BoolToCollapsed : IValueConverter {

        /// <summary>
        ///     Implement IValueConverter.Convert
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        ///     Implement IValueConverter.ConvertBack
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
