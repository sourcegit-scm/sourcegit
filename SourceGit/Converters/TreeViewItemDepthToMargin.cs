using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SourceGit.Converters {

    /// <summary>
    ///     Convert depth of a TreeViewItem to Margin property.
    /// </summary>
    public class TreeViewItemDepthToMargin : IValueConverter {

        /// <summary>
        ///     Indent length
        /// </summary>
        public double Indent { get; set; } = 19;

        /// <summary>
        ///     Implement IValueConverter.Convert
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            TreeViewItem item = value as TreeViewItem;
            if (item == null) return new Thickness(0);

            TreeViewItem iterator = GetParent(item);
            int depth = 0;
            while (iterator != null) {
                depth++;
                iterator = GetParent(iterator);
            }

            return new Thickness(Indent * depth, 0, 0, 0);
        }

        /// <summary>
        ///     Implement IValueConvert.ConvertBack
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Get parent item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private TreeViewItem GetParent(TreeViewItem item) {
            var parent = VisualTreeHelper.GetParent(item);

            while (parent != null && !(parent is TreeView) && !(parent is TreeViewItem)) {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as TreeViewItem;
        }
    }
}
