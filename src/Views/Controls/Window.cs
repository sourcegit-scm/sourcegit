using System;
using System.Windows;
using System.Windows.Documents;
using System.Collections.Generic;

namespace SourceGit.Views.Controls {
    /// <summary>
    ///     项目使用的窗体基类
    /// </summary>
    public class Window : System.Windows.Window {

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
            Loaded += (_, __) => OnStateChanged(null);
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
