using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SourceGit.Views.Converters {

    public class IntToBookmarkBrush : IValueConverter {
        public static readonly Brush[] COLORS = new Brush[] {
            Brushes.Transparent,
            Brushes.Red,
            Brushes.Orange,
            Brushes.Yellow,
            Brushes.ForestGreen,
            Brushes.Purple,
            Brushes.DeepSkyBlue,
            Brushes.Magenta,
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var index = (int)value;
            return COLORS[index];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}