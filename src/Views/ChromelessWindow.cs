using System;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e is { Handled: false, Key: Key.Escape, KeyModifiers: KeyModifiers.None } && CloseOnESC)
            {
                Close();
                e.Handled = true;
            }
        }

        private void OnWindowBorderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border { Tag: WindowEdge edge } && CanResize)
                BeginResizeDrag(edge, e);
        }
    }
}
