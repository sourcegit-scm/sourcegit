using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     标签页图标
    /// </summary>
    public class Bookmark : Border {
        private Path icon = null;

        public static readonly Brush[] COLORS = new Brush[] {
            Brushes.Transparent,
            Brushes.Red,
            Brushes.Orange,
            Brushes.Yellow,
            Brushes.ForestGreen,
            Brushes.Purple,
            Brushes.DeepSkyBlue,
            Brushes.Magenta,
        };

        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(int), typeof(Bookmark), new PropertyMetadata(0, UpdateBookmark));

        public int Color {
            get { return (int)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }

        public static readonly DependencyProperty IsNewPageProperty =
            DependencyProperty.Register("IsNewPage", typeof(bool), typeof(Bookmark), new PropertyMetadata(false, UpdateBookmark));

        public bool IsNewPage {
            get { return (bool)GetValue(IsNewPageProperty); }
            set { SetValue(IsNewPageProperty, value); }
        }

        public Bookmark() {
            icon = new Path();
            Child = icon;
            UpdateBookmark(this, new DependencyPropertyChangedEventArgs());
        }

        private static void UpdateBookmark(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var mark = d as Bookmark;
            if (mark == null) return;

            if (!mark.IsNewPage) {
                mark.icon.Data = mark.FindResource("Icon.Git") as Geometry;
                if (mark.Color == 0) {
                    mark.icon.SetResourceReference(Path.FillProperty, "Brush.FG1");
                } else {
                    mark.icon.Fill = COLORS[mark.Color % COLORS.Length];
                }
            } else {
                mark.icon.SetResourceReference(Path.FillProperty, "Brush.FG1");
                mark.icon.Data = mark.FindResource("Icon.WelcomePage") as Geometry;
            }
        }
    }
}
