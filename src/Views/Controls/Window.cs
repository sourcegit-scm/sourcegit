using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace SourceGit.Views.Controls {
    /// <summary>
    ///     项目使用的窗体基类
    /// </summary>
    public class Window : System.Windows.Window {

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX {
            public int Size;
            public int Major;
            public int Minor;
            public int Build;
            public int Platform;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string CSDVersion;
            public ushort ServicePackMajor;
            public ushort ServicePackMinor;
            public short SuiteMask;
            public byte ProductType;
            public byte Reserved;
        }

        [DllImport("ntdll.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEX version);

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern long DwmSetWindowAttribute(IntPtr hwnd,
            uint attribute,
            ref uint pvAttribute,
            uint cbAttribute);

        public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(
            "IsMaximized",
            typeof(bool),
            typeof(Window),
            new PropertyMetadata(false, OnIsMaximizedChanged));

        public bool IsMaximized {
            get { return (bool)GetValue(IsMaximizedProperty); }
            set { SetValue(IsMaximizedProperty, value); }
        }

        public Window() {
            Style = FindResource("Style.Window") as Style;
            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e) {
            OnStateChanged(null);

            // Windows 11 需要特殊处理一下边框，使得其与Window 10下表现一致
            OSVERSIONINFOEX version = new OSVERSIONINFOEX() { Size = Marshal.SizeOf(typeof(OSVERSIONINFOEX)) };
            if (RtlGetVersion(ref version) == 0 && version.Major >= 10 && version.Build >= 22000) {
                Models.Theme.Changed += UpdateBorderColor;
                Unloaded += (_, __) => Models.Theme.Changed -= UpdateBorderColor;

                UpdateBorderColor();
            }
        }

        private void UpdateBorderColor() {
            IntPtr hWnd = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
            Color color = (BorderBrush as SolidColorBrush).Color;
            uint preference = ((uint)color.B << 16) | ((uint)color.G << 8) | (uint)color.R;
            DwmSetWindowAttribute(hWnd, 34, ref preference, sizeof(uint));
        }

        protected override void OnStateChanged(EventArgs e) {
            if (WindowState == WindowState.Maximized) {
                if (!IsMaximized) IsMaximized = true;
                BorderThickness = new Thickness(0);
                Padding = new Thickness((SystemParameters.MaximizedPrimaryScreenWidth - SystemParameters.WorkArea.Width) / 2);
            } else {
                if (IsMaximized) IsMaximized = false;
                BorderThickness = new Thickness(1);
                Padding = new Thickness(0);
            }
        }

        private static void OnIsMaximizedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            Window w = d as Window;
            if (w != null) {
                if (w.IsMaximized) {
                    SystemCommands.MaximizeWindow(w);
                } else if (w.WindowState != WindowState.Minimized) {
                    SystemCommands.RestoreWindow(w);
                }
            }
        }
    }
}
