using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class WindowStateConverters
    {
        public static readonly FuncValueConverter<WindowState, Thickness> ToContentMargin =
            new FuncValueConverter<WindowState, Thickness>(state =>
            {
                if (OperatingSystem.IsWindows() && state == WindowState.Maximized)
                    return new Thickness(6);
                else if (OperatingSystem.IsLinux() && state != WindowState.Maximized)
                    return new Thickness(6);
                else
                    return new Thickness(0);
            });

        public static readonly FuncValueConverter<WindowState, GridLength> ToTitleBarHeight =
            new FuncValueConverter<WindowState, GridLength>(state =>
            {
                if (state == WindowState.Maximized)
                    return new GridLength(OperatingSystem.IsMacOS() ? 34 : 30);
                else
                    return new GridLength(38);
            });

        public static readonly FuncValueConverter<WindowState, bool> IsNormal =
            new FuncValueConverter<WindowState, bool>(state => state == WindowState.Normal);
    }
}
