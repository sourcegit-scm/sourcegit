using System;
using System.Globalization;
using System.Windows.Data;

namespace SourceGit.Views.Converters {

    public class BranchToName : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var b = value as Models.Branch;
            if (b == null) return "";
            return string.IsNullOrEmpty(b.Remote) ? b.Name : $"{b.Remote}/{b.Name}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
