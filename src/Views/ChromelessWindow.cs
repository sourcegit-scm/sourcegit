using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class ChromelessWindow : Window
    {
        public bool UseSystemWindowFrame
        {
            get => Native.OS.UseSystemWindowFrame;
        }

        public bool CloseOnESC
        {
            get;
            set;
        } = false;

        protected override Type StyleKeyOverride => typeof(Window);

        public ChromelessWindow()
        {
            Focusable = true;
            Native.OS.SetupForWindow(this);
            PositionChanged += (_, _) => UpdateEdgeSnapInsets();
            Resized += (_, _) => UpdateEdgeSnapInsets();
        }

        public void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 1)
                BeginMoveDrag(e);

            e.Handled = true;
        }

        public void MaximizeOrRestoreWindow(object _, TappedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (Classes.Contains("custom_window_frame") && CanResize)
            {
                string[] borderNames = [
                    "PART_BorderTopLeft",
                    "PART_BorderTop",
                    "PART_BorderTopRight",
                    "PART_BorderLeft",
                    "PART_BorderRight",
                    "PART_BorderBottomLeft",
                    "PART_BorderBottom",
                    "PART_BorderBottomRight",
                ];

                foreach (var name in borderNames)
                {
                    var border = e.NameScope.Find<Border>(name);
                    if (border != null)
                    {
                        border.PointerPressed -= OnWindowBorderPointerPressed;
                        border.PointerPressed += OnWindowBorderPointerPressed;
                    }
                }
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (OperatingSystem.IsWindows())
                Native.Win64Utilities.FixWindowFrame(this);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (OperatingSystem.IsWindows() && change.Property == WindowStateProperty)
                Native.Win64Utilities.FixWindowFrame(this);

            if (change.Property == WindowStateProperty)
                UpdateEdgeSnapInsets();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled)
                return;

            if (e is { Key: Key.Escape, KeyModifiers: KeyModifiers.None } && CloseOnESC)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                if (e.Key == Key.OemPlus)
                {
                    var zoom = Math.Min(ViewModels.Preferences.Instance.Zoom + 0.05, 2.5);
                    ViewModels.Preferences.Instance.Zoom = zoom;
                    e.Handled = true;
                }
                else if (e.Key == Key.OemMinus)
                {
                    var zoom = Math.Max(ViewModels.Preferences.Instance.Zoom - 0.05, 1);
                    ViewModels.Preferences.Instance.Zoom = zoom;
                    e.Handled = true;
                }
                else if (e.Key == Key.W)
                {
                    Close();
                    e.Handled = true;
                }
            }
        }

        private void OnWindowBorderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border { Tag: WindowEdge edge } && CanResize)
                BeginResizeDrag(edge, e);
        }

        // The transparent padding around the window holds the self-drawn shadow
        // (keep the value in sync with "Window.custom_window_frame" style).
        // Collapse it on the side touching a screen edge so the content sits flush.
        private void UpdateEdgeSnapInsets()
        {
            if (!Classes.Contains("custom_window_frame"))
                return;

            if (WindowState != WindowState.Normal)
            {
                // Let the "WindowState=Maximized" style take over
                ClearValue(PaddingProperty);
                return;
            }

            var screen = Screens.ScreenFromWindow(this);
            if (screen == null)
                return;

            var workArea = screen.WorkingArea;
            var right = Position.X + Bounds.Width;
            var bottom = Position.Y + Bounds.Height;

            const double tolerance = 2.0;
            var snapTop = Position.Y <= workArea.Y + tolerance;
            var snapLeft = Position.X <= workArea.X + tolerance;
            var snapRight = right >= workArea.Right - tolerance;
            var snapBottom = bottom >= workArea.Bottom - tolerance;

            if (!snapTop && !snapLeft && !snapRight && !snapBottom)
            {
                ClearValue(PaddingProperty);
                return;
            }

            Padding = new Thickness(
                snapLeft ? 0 : 12,
                snapTop ? 0 : 12,
                snapRight ? 0 : 12,
                snapBottom ? 0 : 12);
        }
    }
}
