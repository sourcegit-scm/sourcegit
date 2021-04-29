using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     简化只有一个Icon的Button
    /// </summary>
    public class IconButton : Button {

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon", 
            typeof(Geometry), 
            typeof(IconButton), 
            new PropertyMetadata(null));

        public Geometry Icon {
            get { return (Geometry)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.Register(
            "HoverBackground", 
            typeof(Brush), 
            typeof(IconButton), 
            new PropertyMetadata(Brushes.Transparent));

        public Brush HoverBackground {
            get { return (Brush)GetValue(HoverBackgroundProperty); }
            set { SetValue(HoverBackgroundProperty, value); }
        }
    }
}
