using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class WindowStateConverters
    {
        public static FuncValueConverter<WindowState, Thickness> ToContentMargin =
            new FuncValueConverter<WindowState, Thickness>(state =>
            {
                if (OperatingSystem.IsWindows() && state == WindowState.Maximized)
                {
                    return new Thickness(6);
                }
                else if (OperatingSystem.IsLinux() && state != WindowState.Maximized)
                {
                    return new Thickness(6);
                }
                else
                {
                    return new Thickness(0);
                }
            });

        public static FuncValueConverter<WindowState, GridLength> ToTitleBarHeight =
            new FuncValueConverter<WindowState, GridLength>(state =>
            {
                if (state == WindowState.Maximized)
                {
                    return new GridLength(30);
                }
                else
                {
                    return new GridLength(38);
                }
            });

        public static FuncValueConverter<WindowState, StreamGeometry> ToMaxOrRestoreIcon =
            new FuncValueConverter<WindowState, StreamGeometry>(state =>
            {
                if (state == WindowState.Maximized)
                {
                    return Application.Current?.FindResource("Icons.Window.Restore") as StreamGeometry;
                }
                else
                {
                    return Application.Current?.FindResource("Icons.Window.Maximize") as StreamGeometry;
                }
            });

        public static FuncValueConverter<WindowState, bool> IsNormal =
            new FuncValueConverter<WindowState, bool>(state => state == WindowState.Normal);
    }
}