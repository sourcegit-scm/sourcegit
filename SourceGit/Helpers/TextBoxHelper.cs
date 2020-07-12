using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Attached properties to TextBox.
    /// </summary>
    public static class TextBoxHelper {

        /// <summary>
        ///     Auto scroll on text changed.
        /// </summary>
        public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.RegisterAttached(
            "AutoScroll",
            typeof(bool),
            typeof(TextBoxHelper),
            new PropertyMetadata(false, OnAutoScrollChanged));

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
        ///     Setter for AutoScrollProperty
        /// </summary>
        /// <param name="element"></param>
        /// <param name="enabled"></param>
        public static void SetAutoScroll(UIElement element, bool enabled) {
            element.SetValue(AutoScrollProperty, enabled);
        }

        /// <summary>
        ///     Getter for AutoScrollProperty
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static bool GetAutoScroll(UIElement element) {
            return (bool)element.GetValue(AutoScrollProperty);
        }

        /// <summary>
        ///     Triggered when AutoScroll property changed.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        public static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var textBox = d as TextBox;
            if (textBox == null) return;

            textBox.SelectionChanged -= UpdateScrollOnSelectionChanged;
            if ((bool)e.NewValue == true) {
                textBox.SelectionChanged += UpdateScrollOnSelectionChanged;
            }
        }

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
            textBox.TextChanged += UpdatePlaceholder;
            UpdatePlaceholder(textBox, null);
        }

        /// <summary>
        ///     Dynamically hide/show placeholder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void UpdatePlaceholder(object sender, RoutedEventArgs e) {
            var textBox = sender as TextBox;
            if (string.IsNullOrEmpty(textBox.Text)) {
                textBox.Background = textBox.GetValue(PlaceholderBrushProperty) as Brush;
            } else {
                textBox.Background = Brushes.Transparent;
            }
        }

        /// <summary>
        ///     
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void UpdateScrollOnSelectionChanged(object sender, RoutedEventArgs e) {
            var textBox = sender as TextBox;
            if (textBox != null && textBox.IsFocused) {
                if (Mouse.LeftButton == MouseButtonState.Pressed && textBox.SelectionLength > 0) {
                    var p = Mouse.GetPosition(textBox);
                    if (p.X <= 8) {
                        textBox.LineLeft();
                    } else if (p.X >= textBox.ActualWidth - 8) {
                        textBox.LineRight();
                    }

                    if (p.Y <= 8) {
                        textBox.LineUp();
                    } else if (p.Y >= textBox.ActualHeight - 8) {
                        textBox.LineDown();
                    }
                } else {
                    var rect = textBox.GetRectFromCharacterIndex(textBox.CaretIndex);
                    if (rect.Left <= 0) {
                        textBox.ScrollToHorizontalOffset(textBox.HorizontalOffset + rect.Left);
                    } else if (rect.Right >= textBox.ActualWidth) {
                        textBox.ScrollToHorizontalOffset(textBox.HorizontalOffset + rect.Right);
                    }

                    if (rect.Top <= 0) {
                        textBox.ScrollToVerticalOffset(textBox.VerticalOffset + rect.Top);
                    } else if (rect.Bottom >= textBox.ActualHeight) {
                        textBox.ScrollToVerticalOffset(textBox.VerticalOffset + rect.Bottom);
                    }
                    
                }
            }
        }
    }
}
