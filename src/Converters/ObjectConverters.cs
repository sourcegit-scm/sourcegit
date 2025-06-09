using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class ObjectConverters
    {
        public class IsTypeOfConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null || parameter == null)
                    return false;

                return value.GetType().IsAssignableTo((Type)parameter);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return new NotImplementedException();
            }
        }

        public static readonly IsTypeOfConverter IsTypeOf = new IsTypeOfConverter();
    }
}
