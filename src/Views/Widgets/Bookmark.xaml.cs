using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     仓库书签
    /// </summary>
    public partial class Bookmark : UserControl {
        /// <summary>
        ///     颜色属性
        /// </summary>
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
            "Color",
            typeof(int),
            typeof(Bookmark),
            new PropertyMetadata(0));

        /// <summary>
        ///     颜色
        /// </summary>
        public int Color {
            get { return (int)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }

        /// <summary>
        ///     构造函数
        /// </summary>
        public Bookmark() {
            InitializeComponent();
        }
    }
}
