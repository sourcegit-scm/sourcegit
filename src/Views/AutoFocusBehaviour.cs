using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public class AutoFocusBehaviour : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<AutoFocusBehaviour, TextBox, bool>("IsEnabled", false, false);

        static AutoFocusBehaviour()
        {
            IsEnabledProperty.Changed.AddClassHandler<TextBox>(OnIsEnabledChanged);
        }

        public static bool GetIsEnabled(AvaloniaObject elem)
        {
            return elem.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(AvaloniaObject elem, bool value)
        {
            elem.SetValue(IsEnabledProperty, value);
        }

        private static void OnIsEnabledChanged(TextBox elem, AvaloniaPropertyChangedEventArgs e)
        {
            if (GetIsEnabled(elem))
            {
                elem.AttachedToVisualTree += (o, _) =>
                {
                    var text = o as TextBox;
                    text.Focus(NavigationMethod.Directional);
                    text.CaretIndex = text.Text == null ? 0 : text.Text.Length;
                };
            }
        }
    }
}
