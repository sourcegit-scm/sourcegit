using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SourceGit.Converters {

    /// <summary>
    ///     Integer to color.
    /// </summary>
    public class IntToRepoColor : IValueConverter {
        
        /// <summary>
        ///     All supported colors.
        /// </summary>
        public static Brush[] Colors = new Brush[] {
            Brushes.Transparent,
            Brushes.White,
            Brushes.Red,
            Brushes.Orange,
            Brushes.Yellow,
            Brushes.ForestGreen,
            Brushes.Purple,
            Brushes.DeepSkyBlue,
            Brushes.Magenta,
        };

        /// <summary>
        ///     Implement IValueConverter.Convert
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Colors[((int)value) % Colors.Length];
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
