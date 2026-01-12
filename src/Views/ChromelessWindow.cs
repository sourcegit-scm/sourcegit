using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace SourceGit.Views
{
    public class ChromelessWindow : Window
    {
        public static readonly StyledProperty<double> LeftCaptionButtonWidthProperty =
            AvaloniaProperty.Register<ChromelessWindow, double>(nameof(LeftCaptionButtonWidth), 72.0);

        public double LeftCaptionButtonWidth
        {
            get => GetValue(LeftCaptionButtonWidthProperty);
            set => SetValue(LeftCaptionButtonWidthProperty, value);
        }

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
            LeftCaptionButtonWidth = 72.0 / Math.Max(1.0, ViewModels.Preferences.Instance.Zoom);
            Focusable = true;
            Native.OS.SetupForWindow(this);
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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty && OperatingSystem.IsWindows())
            {
                if (WindowState == WindowState.Maximized)
                {
                    BorderThickness = new Thickness(0);
                    Padding = new Thickness(8, 6, 8, 8);
                }
                else
                {
                    BorderThickness = new Thickness(1);
                    Padding = new Thickness(0);
                }
            }
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
                    LeftCaptionButtonWidth = 72.0 / zoom;
                    e.Handled = true;
                }
                else if (e.Key == Key.OemMinus)
                {
                    var zoom = Math.Max(ViewModels.Preferences.Instance.Zoom - 0.05, 1);
                    ViewModels.Preferences.Instance.Zoom = zoom;
                    LeftCaptionButtonWidth = 72.0 / zoom;
                    e.Handled = true;
                }
            }
        }

        private void OnWindowBorderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border { Tag: WindowEdge edge } && CanResize)
                BeginResizeDrag(edge, e);
        }
    }
}
