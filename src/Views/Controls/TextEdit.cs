using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     扩展默认TextBox
    /// </summary>
    public class TextEdit : TextBox {
        private bool isPlaceholderShow = false;

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            "Placeholder",
            typeof(string),
            typeof(TextEdit),
            new PropertyMetadata(""));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderBaselineProperty = DependencyProperty.Register(
            "PlaceholderBaseline",
            typeof(AlignmentY),
            typeof(TextEdit),
            new PropertyMetadata(AlignmentY.Center));

        public AlignmentY PlaceholderBaseline {
            get { return (AlignmentY)GetValue(PlaceholderBaselineProperty); }
            set { SetValue(PlaceholderBaselineProperty, value); }
        }

        public TextEdit() {
            TextChanged += OnTextChanged;
            SelectionChanged += OnSelectionChanged;
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder)) {
                isPlaceholderShow = true;

                var text = new FormattedText(
                    Placeholder,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                    FontSize,
                    FindResource("Brush.FG2") as Brush,
                    new NumberSubstitution(),
                    TextFormattingMode.Display,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                switch (PlaceholderBaseline) {
                case AlignmentY.Top:
                    dc.DrawText(text, new Point(4, 4));
                    break;
                case AlignmentY.Center:
                    dc.DrawText(text, new Point(4, ActualHeight * .5 - text.Height * .5));
                    break;
                default:
                    dc.DrawText(text, new Point(4, ActualHeight - text.Height - 4));
                    break;
                }
            } else {
                isPlaceholderShow = false;
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e) {
            if (string.IsNullOrEmpty(Text) || isPlaceholderShow) InvalidateVisual();
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e) {
            if (!IsFocused) return;

            if (Mouse.LeftButton == MouseButtonState.Pressed && SelectionLength > 0) {
                var p = Mouse.GetPosition(this);
                if (p.X <= 8) {
                    LineLeft();
                } else if (p.X >= ActualWidth - 8) {
                    LineRight();
                }

                if (p.Y <= 8) {
                    LineUp();
                } else if (p.Y >= ActualHeight - 8) {
                    LineDown();
                }
            } else {
                var rect = GetRectFromCharacterIndex(CaretIndex);
                if (rect.Left <= 0) {
                    ScrollToHorizontalOffset(HorizontalOffset + rect.Left);
                } else if (rect.Right >= ActualWidth) {
                    ScrollToHorizontalOffset(HorizontalOffset + rect.Right);
                }

                if (rect.Top <= 0) {
                    ScrollToVerticalOffset(VerticalOffset + rect.Top);
                } else if (rect.Bottom >= ActualHeight) {
                    ScrollToVerticalOffset(VerticalOffset + rect.Bottom);
                }
            }
        }
    }
}
