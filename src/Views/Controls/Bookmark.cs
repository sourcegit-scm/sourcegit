using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            Brushes.White,
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

        public static readonly DependencyProperty HideOnZeroProperty =
            DependencyProperty.Register("HideOnZero", typeof(bool), typeof(Bookmark), new PropertyMetadata(false, UpdateBookmark));

        public bool HideOnZero {
            get { return (bool)GetValue(HideOnZeroProperty); }
            set { SetValue(HideOnZeroProperty, value); }
        }

        public Bookmark() {
            icon = new Path();
            Child = icon;
            UpdateBookmark(this, new DependencyPropertyChangedEventArgs());
        }

        private static void UpdateBookmark(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var mark = d as Bookmark;
            if (mark == null) return;

            if (mark.HideOnZero && mark.Color == 0) {
                mark.Visibility = Visibility.Collapsed;
                return;
            }

            if (!mark.IsNewPage) {                
                if (mark.Color == 0) {
                    mark.icon.Fill = mark.FindResource("Brush.FG1") as Brush;
                    mark.icon.Data = mark.FindResource("Icon.Git") as Geometry;
                } else {
                    mark.icon.Fill = COLORS[mark.Color % COLORS.Length];
                    mark.icon.Data = mark.FindResource("Icon.Bookmark") as Geometry;
                }
            } else {
                mark.icon.Fill = mark.FindResource("Brush.FG1") as Brush;
                mark.icon.Data = mark.FindResource("Icon.NewPage") as Geometry;
            }

            mark.Visibility = Visibility.Visible;
        }
    }
}
