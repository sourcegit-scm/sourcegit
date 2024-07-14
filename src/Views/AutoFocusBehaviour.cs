using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public class AutoFocusBehaviour : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<AutoFocusBehaviour, TextBox, bool>("IsEnabled");

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
                    if (o is TextBox box)
                    {
                        box.Focus(NavigationMethod.Directional);
                        box.CaretIndex = box.Text?.Length ?? 0;
                    }
                };
            }
        }
    }
}
