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

        private AdornerLayer adornerLayer = null;
        private List<Adorner> adorners = new List<Adorner>();

        public Window() {
            Style = FindResource("Style.Window") as Style;
            Loaded += (_, __) => adornerLayer = AdornerLayer.GetAdornerLayer(Content as FrameworkElement);
        }

        public static void AddAdorner(FrameworkElement windowContext, Adorner adorner) {
            var wnd = GetWindow(windowContext) as Window;
            if (wnd != null && wnd.adornerLayer != null) {
                wnd.adorners.Add(adorner);
                wnd.adornerLayer.Add(adorner);
            }
        }

        public static void RemoveAdorner(FrameworkElement windowContext, Adorner adorner) {
            var wnd = GetWindow(windowContext) as Window;
            if (wnd != null && wnd.adornerLayer != null) {
                wnd.adorners.Remove(adorner);
                wnd.adornerLayer.Remove(adorner);
            }
        }

        protected override void OnPreviewGiveFeedback(GiveFeedbackEventArgs e) {
            base.OnPreviewGiveFeedback(e);
            if (adornerLayer != null && adorners.Count > 0) adornerLayer.Update();
        }

        protected override void OnStateChanged(EventArgs e) {
            base.OnStateChanged(e);

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
