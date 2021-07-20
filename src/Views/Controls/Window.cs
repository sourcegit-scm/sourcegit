using System.Windows;

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

            StateChanged += (_, __) => {
                var content = Content as FrameworkElement;

                if (WindowState == WindowState.Maximized) {
                    if (!IsMaximized) IsMaximized = true;
                    content.Margin = new Thickness((SystemParameters.MaximizedPrimaryScreenWidth - SystemParameters.WorkArea.Width) / 2);
                } else {
                    if (IsMaximized) IsMaximized = false;
                    content.Margin = new Thickness(0);
                }
            };
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
