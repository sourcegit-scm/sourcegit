using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     树节点
    /// </summary>
    public class TreeItem : TreeViewItem {

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            "IsChecked",
            typeof(bool),
            typeof(TreeItem),
            new PropertyMetadata(false));

        public bool IsChecked {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        private int depth = 0;
        private double indent = 16;

        public TreeItem(int depth, double indent) {
            this.depth = depth;
            this.indent = indent;

            Padding = new Thickness(indent * depth, 0, 0, 0);
        }

        protected override DependencyObject GetContainerForItemOverride() {
            return new TreeItem(depth + 1, indent);
        }

        protected override bool IsItemItsOwnContainerOverride(object item) {
            return item is TreeItem;
        }
    }
}
