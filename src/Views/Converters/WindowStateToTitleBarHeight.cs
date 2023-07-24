using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SourceGit.Views.Converters {

    /// <summary>
    ///     将当前窗口的状态转换为标题栏高度
    /// </summary>
    public class WindowStateToTitleBarHeight : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            WindowState state = (WindowState)value;
            return state == WindowState.Normal ? 36 : 30;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}