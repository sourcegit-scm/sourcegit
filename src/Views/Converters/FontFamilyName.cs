using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace SourceGit.Views.Converters {

    public class FontFamiliesToName : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string s)
                return s.Split(',').ElementAt(0);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value;
        }
    }
}
