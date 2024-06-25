using System;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class BoolConverters
    {
        public static readonly FuncValueConverter<bool, double> ToPageTabWidth =
            new FuncValueConverter<bool, double>(x => x ? 200 : double.NaN);

        public static readonly FuncValueConverter<bool, double> HalfIfFalse =
            new FuncValueConverter<bool, double>(x => x ? 1 : 0.5);

        public static readonly FuncValueConverter<bool, FontWeight> BoldIfTrue =
            new FuncValueConverter<bool, FontWeight>(x => x ? FontWeight.Bold : FontWeight.Regular);

        public class InverseConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return !(bool)value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return !(bool)value;
            }
        }

        public static readonly InverseConverter Inverse = new InverseConverter();

    }
}
