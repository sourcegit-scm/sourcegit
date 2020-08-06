using System;
using System.Globalization;
using System.Windows.Data;

namespace SourceGit.Converters {

    /// <summary>
    ///     Convert percent to double.
    /// </summary>
    public class PercentToDouble : IValueConverter {

        /// <summary>
        ///     Percentage.
        /// </summary>
        public double Percent { get; set; }

        /// <summary>
        ///     Implement IValueConverter.Convert
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (double)value * Percent;
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
