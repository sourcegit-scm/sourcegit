using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SourceGit.Views
{
    public class MenuItemExtension : AvaloniaObject
    {
        public static readonly AttachedProperty<string> CommandProperty =
            AvaloniaProperty.RegisterAttached<MenuItemExtension, MenuItem, string>("Command", string.Empty, false, BindingMode.OneWay);
    }
}
