using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SourceGit.Converters {
    public static class IntConverters {
        public static FuncValueConverter<int, bool> IsGreaterThanZero =
            new FuncValueConverter<int, bool>(v => v > 0);

        public static FuncValueConverter<int, bool> IsZero =
            new FuncValueConverter<int, bool>(v => v == 0);

        public class NotEqualConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                int v = (int)value;
                int target = (int)parameter;
                return v != target;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotImplementedException();
            }
        }

        public static NotEqualConverter NotEqual = new NotEqualConverter();
    }
}
