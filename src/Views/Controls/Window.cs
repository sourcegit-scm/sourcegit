using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;

namespace SourceGit.Views.Controls {
    /// <summary>
    ///     项目使用的窗体基类
    /// </summary>
    public class Window : System.Windows.Window {

        public Window() {
            Background = FindResource("Brush.Window") as Brush;
            BorderBrush = FindResource("Brush.WindowBorder") as Brush;
            BorderThickness = new Thickness(1);

            SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);
            SetValue(TextOptions.TextRenderingModeProperty, TextRenderingMode.ClearType);
            SetValue(TextOptions.TextHintingModeProperty, TextHintingMode.Animated);
            UseLayoutRounding = true;

            var chrome = new WindowChrome();
            chrome.ResizeBorderThickness = new Thickness(4);
            chrome.UseAeroCaptionButtons = false;
            chrome.CornerRadius = new CornerRadius(0);
            chrome.CaptionHeight = 28;
            WindowChrome.SetWindowChrome(this, chrome);

            StateChanged += (_, __) => {
                var content = Content as FrameworkElement;

                if (WindowState == WindowState.Maximized) {
                    BorderThickness = new Thickness(0);
                    content.Margin = new Thickness((SystemParameters.MaximizedPrimaryScreenWidth - SystemParameters.WorkArea.Width) / 2);
                } else {
                    BorderThickness = new Thickness(1);
                    content.Margin = new Thickness(0);
                }
            };
        }
    }
}
