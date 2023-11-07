using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     扩展默认TextBox
    /// </summary>
    public class TextEdit : TextBox {
        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            "Placeholder",
            typeof(string),
            typeof(TextEdit),
            new PropertyMetadata(""));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderVisibilityProperty = DependencyProperty.Register(
            "PlaceholderVisibility",
            typeof(Visibility),
            typeof(TextEdit),
            new PropertyMetadata(Visibility.Visible));

        public Visibility PlaceholderVisibility {
            get { return (Visibility)GetValue(PlaceholderVisibilityProperty); }
            set { SetValue(PlaceholderVisibilityProperty, value); }
        }

        public TextEdit() {
            TextChanged += OnTextChanged;
            SelectionChanged += OnSelectionChanged;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e) {
            base.OnMouseWheel(e);

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
                if (e.Delta > 0) {
                    LineLeft();
                } else {
                    LineRight();
                }
            } else {
                if (e.Delta > 0) {
                    LineUp();
                } else {
                    LineDown();
                }
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e) {
            PlaceholderVisibility = string.IsNullOrEmpty(Text) ? Visibility.Visible : Visibility.Collapsed;
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
