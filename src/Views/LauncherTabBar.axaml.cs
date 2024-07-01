using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SourceGit.Views
{
    public partial class LauncherTabBar : UserControl
    {
        public LauncherTabBar()
        {
            InitializeComponent();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (LauncherTabsList == null || LauncherTabsList.SelectedIndex == -1)
                return;

            var startX = LauncherTabsScroller.Offset.X;
            var endX = startX + LauncherTabsScroller.Viewport.Width;
            var height = LauncherTabsScroller.Viewport.Height;

            var selectedIdx = LauncherTabsList.SelectedIndex;
            var count = LauncherTabsList.ItemCount;
            var separatorPen = new Pen(this.FindResource("Brush.FG2") as IBrush, 0.5);
            var separatorY = (height - 20) * 0.5;
            for (var i = 0; i < count; i++)
            {
                if (i == selectedIdx || i == selectedIdx - 1)
                    continue;

                var container = LauncherTabsList.ContainerFromIndex(i);
                var containerEndX = container.Bounds.Right;
                if (containerEndX < startX || containerEndX > endX)
                    continue;

                var separatorX = containerEndX - startX + LauncherTabsScroller.Bounds.X;
                context.DrawLine(separatorPen, new Point(separatorX, separatorY), new Point(separatorX, separatorY + 20));
            }

            var selected = LauncherTabsList.ContainerFromIndex(selectedIdx);
            var activeStartX = selected.Bounds.X;
            var activeEndX = activeStartX + selected.Bounds.Width;
            if (activeStartX > endX + 5 || activeEndX < startX - 5)
                return;

            var geo = new StreamGeometry();
            var angle = Math.PI / 2;
            var x = 0.0;
            var y = height + 0.25;
            using (var ctx = geo.Open())
            {
                var drawLeftX = activeStartX - startX + LauncherTabsScroller.Bounds.X;
                var drawRightX = activeEndX - startX + LauncherTabsScroller.Bounds.X;
                if (drawLeftX < LauncherTabsScroller.Bounds.X)
                {
                    x = LauncherTabsScroller.Bounds.X;
                    ctx.BeginFigure(new Point(x, y), true);
                    y = 1;
                    ctx.LineTo(new Point(x, y));
                    x = drawRightX - 6;
                }
                else
                {
                    x = drawLeftX - 5;
                    ctx.BeginFigure(new Point(x, y), true);
                    x = drawLeftX;
                    y -= 5;
                    ctx.ArcTo(new Point(x, y), new Size(5, 5), angle, false, SweepDirection.CounterClockwise);
                    y = 6;
                    ctx.LineTo(new Point(x, y));
                    x += 6;
                    y = 1;
                    ctx.ArcTo(new Point(x, y), new Size(6, 6), angle, false, SweepDirection.Clockwise);
                    x = drawRightX - 6;
                }

                if (drawRightX < LauncherTabsScroller.Bounds.Right)
                {
                    ctx.LineTo(new Point(x, y));
                    x = drawRightX;
                    y = 6;
                    ctx.ArcTo(new Point(x, y), new Size(6, 6), angle, false, SweepDirection.Clockwise);
                    y = height + 0.25 - 5;
                    ctx.LineTo(new Point(x, y));
                    x += 5;
                    y = height + 0.25;
                    ctx.ArcTo(new Point(x, y), new Size(5, 5), angle, false, SweepDirection.CounterClockwise);
                }
                else
                {
                    x = LauncherTabsScroller.Bounds.Right;
                    ctx.LineTo(new Point(x, y));
                    y = height + 0.25;
                    ctx.LineTo(new Point(x, y));
                }
            }

            var fill = this.FindResource("Brush.ToolBar") as IBrush;
            var stroke = new Pen(this.FindResource("Brush.Border0") as IBrush, 1);
            context.DrawGeometry(fill, stroke, geo);
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

            InvalidateVisual();
        }

        private void OnTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InvalidateVisual();
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
