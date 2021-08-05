using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     徽章
    /// </summary>
    public class Badge : Border {
        private TextBlock label = null;

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            "Label",
            typeof(string),
            typeof(Border),
            new PropertyMetadata("", OnLabelChanged));

        public string Label {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public Badge() {
            Width = double.NaN;
            Height = 18;
            CornerRadius = new CornerRadius(9);
            VerticalAlignment = VerticalAlignment.Center;
            Visibility = Visibility.Collapsed;

            SetResourceReference(BackgroundProperty, "Brush.Badge");

            label = new TextBlock();
            label.FontSize = 10;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(9, 0, 9, 0);
            Child = label;
        }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            Badge badge = d as Badge;
            if (badge != null) {
                var text = e.NewValue as string;
                if (string.IsNullOrEmpty(text) || text == "0") {
                    badge.Visibility = Visibility.Collapsed;
                } else {
                    badge.label.Text = text;
                    badge.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
