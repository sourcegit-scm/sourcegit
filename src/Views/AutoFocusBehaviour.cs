using Avalonia;
using Avalonia.Input;

namespace SourceGit.Views
{
    public class AutoFocusBehaviour : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<AutoFocusBehaviour, InputElement, bool>("IsEnabled", false, false);

        static AutoFocusBehaviour()
        {
            IsEnabledProperty.Changed.AddClassHandler<InputElement>((input, e) =>
            {
                if (input.GetValue(IsEnabledProperty))
                {
                    input.AttachedToVisualTree += (o, _) => (o as InputElement).Focus(NavigationMethod.Directional);
                }
            });
        }

        public static bool GetIsEnabled(AvaloniaObject elem)
        {
            return elem.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(AvaloniaObject elem, bool value)
        {
            elem.SetValue(IsEnabledProperty, value);
        }
    }
}
