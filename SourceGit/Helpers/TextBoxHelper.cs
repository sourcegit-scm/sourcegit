using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Attached properties to TextBox.
    /// </summary>
    public static class TextBoxHelper {

        /// <summary>
        ///     Placeholder property  
        /// </summary>
        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(TextBoxHelper),
            new PropertyMetadata(string.Empty, OnPlaceholderChanged));

        /// <summary>
        ///     Vertical alignment for placeholder.
        /// </summary>
        public static readonly DependencyProperty PlaceholderBaselineProperty = DependencyProperty.RegisterAttached(
            "PlaceholderBaseline",
            typeof(AlignmentY),
            typeof(TextBoxHelper),
            new PropertyMetadata(AlignmentY.Center));

        /// <summary>
        ///     Property to store generated placeholder brush.
        /// </summary>
        public static readonly DependencyProperty PlaceholderBrushProperty = DependencyProperty.RegisterAttached(
            "PlaceholderBrush",
            typeof(Brush),
            typeof(TextBoxHelper),
            new PropertyMetadata(Brushes.Transparent));

        /// <summary>
        ///     Triggered when placeholder changed.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var textBox = d as TextBox;
            if (textBox != null) textBox.Loaded += OnTextLoaded;
        }

        /// <summary>
        ///     Setter for Placeholder property
        /// </summary>
        /// <param name="element"></param>
        /// <param name="value"></param>
        public static void SetPlaceholder(UIElement element, string value) {
            element.SetValue(PlaceholderProperty, value);
        }

        /// <summary>
        ///     Getter for Placeholder property
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static string GetPlaceholder(UIElement element) {
            return (string)element.GetValue(PlaceholderProperty);
        }

        /// <summary>
        ///     Setter for PlaceholderBaseline property
        /// </summary>
        /// <param name="element"></param>
        /// <param name="align"></param>
        public static void SetPlaceholderBaseline(UIElement element, AlignmentY align) {
            element.SetValue(PlaceholderBaselineProperty, align);
        }

        /// <summary>
        ///     Setter for PlaceholderBaseline property.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static AlignmentY GetPlaceholderBaseline(UIElement element) {
            return (AlignmentY)element.GetValue(PlaceholderBaselineProperty);
        }

        /// <summary>
        ///     Setter for PlaceholderBrush property.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="value"></param>
        public static void SetPlaceholderBrush(UIElement element, Brush value) {
            element.SetValue(PlaceholderBrushProperty, value);
        }

        /// <summary>
        ///     Getter for PlaceholderBrush property.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static Brush GetPlaceholderBrush(UIElement element) {
            return (Brush)element.GetValue(PlaceholderBrushProperty);
        }

        /// <summary>
        ///     Set placeholder as background when TextBox was loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnTextLoaded(object sender, RoutedEventArgs e) {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            Label placeholder = new Label();
            placeholder.Content = textBox.GetValue(PlaceholderProperty);

            VisualBrush brush = new VisualBrush();
            brush.AlignmentX = AlignmentX.Left;
            brush.AlignmentY = GetPlaceholderBaseline(textBox);
            brush.TileMode = TileMode.None;
            brush.Stretch = Stretch.None;
            brush.Opacity = 0.3;
            brush.Visual = placeholder;

            textBox.SetValue(PlaceholderBrushProperty, brush);
            textBox.Background = brush;
            textBox.TextChanged += OnTextChanged;
            OnTextChanged(textBox, null);
        }

        /// <summary>
        ///     Dynamically hide/show placeholder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnTextChanged(object sender, RoutedEventArgs e) {
            var textBox = sender as TextBox;
            if (string.IsNullOrEmpty(textBox.Text)) {
                textBox.Background = textBox.GetValue(PlaceholderBrushProperty) as Brush;
            } else {
                textBox.Background = Brushes.Transparent;
            }
        }
    }
}
