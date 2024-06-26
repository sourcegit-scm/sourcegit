using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class LauncherTabBar : UserControl
    {
        public LauncherTabBar()
        {
            InitializeComponent();
        }

        private void ScrollTabs(object sender, PointerWheelEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (e.Delta.Y < 0)
                    LauncherTabsScroller.LineRight();
                else if (e.Delta.Y > 0)
                    LauncherTabsScroller.LineLeft();
                e.Handled = true;
            }
        }

        private void ScrollTabsLeft(object sender, RoutedEventArgs e)
        {
            LauncherTabsScroller.LineLeft();
            e.Handled = true;
        }

        private void ScrollTabsRight(object sender, RoutedEventArgs e)
        {
            LauncherTabsScroller.LineRight();
            e.Handled = true;
        }

        private void OnTabsLayoutUpdated(object sender, EventArgs e)
        {
            if (LauncherTabsScroller.Extent.Width > LauncherTabsScroller.Viewport.Width)
            {
                LeftScrollIndicator.IsVisible = true;
                LeftScrollIndicator.IsEnabled = LauncherTabsScroller.Offset.X > 0;
                RightScrollIndicator.IsVisible = true;
                RightScrollIndicator.IsEnabled = LauncherTabsScroller.Offset.X < LauncherTabsScroller.Extent.Width - LauncherTabsScroller.Viewport.Width;
            }
            else
            {
                LeftScrollIndicator.IsVisible = false;
                RightScrollIndicator.IsVisible = false;
            }
        }

        private void SetupDragAndDrop(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                DragDrop.SetAllowDrop(border, true);
                border.AddHandler(DragDrop.DropEvent, DropTab);
            }
            e.Handled = true;
        }

        private void OnPointerPressedTab(object sender, PointerPressedEventArgs e)
        {
            var border = sender as Border;
            var point = e.GetCurrentPoint(border);
            if (point.Properties.IsMiddleButtonPressed)
            {
                var vm = DataContext as ViewModels.Launcher;
                vm.CloseTab(border.DataContext as ViewModels.LauncherPage);
                e.Handled = true;
                return;
            }

            _pressedTab = true;
            _startDragTab = false;
            _pressedTabPosition = e.GetPosition(sender as Border);
        }

        private void OnPointerReleasedTab(object sender, PointerReleasedEventArgs e)
        {
            _pressedTab = false;
            _startDragTab = false;
        }

        private void OnPointerMovedOverTab(object sender, PointerEventArgs e)
        {
            if (_pressedTab && !_startDragTab && sender is Border border)
            {
                var delta = e.GetPosition(border) - _pressedTabPosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTab = true;

                var data = new DataObject();
                data.Set("MovedTab", border.DataContext);
                DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            }
            e.Handled = true;
        }

        private void DropTab(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedTab") && sender is Border border)
            {
                var to = border.DataContext as ViewModels.LauncherPage;
                var moved = e.Data.Get("MovedTab") as ViewModels.LauncherPage;
                if (to != null && moved != null && to != moved && DataContext is ViewModels.Launcher vm)
                {
                    vm.MoveTab(moved, to);
                }
            }

            _pressedTab = false;
            _startDragTab = false;
            e.Handled = true;
        }

        private void OnTabContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Border border && DataContext is ViewModels.Launcher vm)
            {
                var menu = vm.CreateContextForPageTab(border.DataContext as ViewModels.LauncherPage);
                border.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnCloseTab(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is ViewModels.Launcher vm)
                vm.CloseTab(btn.DataContext as ViewModels.LauncherPage);

            e.Handled = true;
        }

        private bool _pressedTab = false;
        private Point _pressedTabPosition = new Point();
        private bool _startDragTab = false;
    }
}
