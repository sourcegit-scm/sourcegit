using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace SourceGit.Views.Converters {

    /// <summary>
    ///     将路径转换为纯文件名
    /// </summary>
    public class PureFileName : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Path.GetFileName(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
