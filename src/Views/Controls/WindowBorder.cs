using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     主窗体Border
    /// </summary>
    public class WindowBorder : Border {

        public WindowBorder() {
            Background = FindResource("Brush.Window") as Brush;
            BorderBrush = FindResource("Brush.WindowBorder") as Brush;
            BorderThickness = new Thickness(1);
            Margin = new Thickness(0);

            Loaded += (o, e) => {
                var owner = Parent as Window;
                if (owner != null) {
                    owner.StateChanged += (o1, e1) => {
                        if (owner.WindowState == WindowState.Maximized) {
                            BorderThickness = new Thickness(0);
                            Margin = new Thickness(
                                (SystemParameters.MaximizedPrimaryScreenWidth - SystemParameters.WorkArea.Width) / 2
                            );
                        } else {
                            BorderThickness = new Thickness(1);
                            Margin = new Thickness(0);
                        }
                    };
                }
            };
        }
    }
}
