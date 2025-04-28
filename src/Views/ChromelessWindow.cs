﻿using System;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;

namespace SourceGit.Views
{
    public class ChromelessWindow : Window
    {
        public bool UseSystemWindowFrame
        {
            get => OperatingSystem.IsLinux() && ViewModels.Preferences.Instance.UseSystemWindowFrame;
        }

        protected override Type StyleKeyOverride => typeof(Window);

        public ChromelessWindow()
        {
            Focusable = true;

            if (OperatingSystem.IsLinux())
            {
                if (UseSystemWindowFrame)
                {
                    ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.Default;
                    ExtendClientAreaToDecorationsHint = false;
                }
                else
                {
                    ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
                    ExtendClientAreaToDecorationsHint = true;
                    Classes.Add("custom_window_frame");
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
                ExtendClientAreaToDecorationsHint = true;
                Classes.Add("fix_maximized_padding");
            }
            else
            {
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome;
                ExtendClientAreaToDecorationsHint = true;
            }
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

        private void OnWindowBorderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is WindowEdge edge && CanResize)
                BeginResizeDrag(edge, e);
        }
    }
}
