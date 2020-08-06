using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SourceGit.Converters {

    /// <summary>
    ///     Convert indent(horizontal offset) to Margin property
    /// </summary>
    public class IndentToMargin : IValueConverter {

        /// <summary>
        ///     Implement IValueConverter.Convert
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return new Thickness((double)value, 0, 0, 0);
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
            return ((Thickness)value).Left;
        }
    }
}
