using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace SourceGit.Views
{
    public class LauncherTabSizeBox : Border
    {
        public static readonly DirectProperty<LauncherTabSizeBox, bool> UseFixedWidthProperty =
            AvaloniaProperty.RegisterDirect<LauncherTabSizeBox, bool>(
                nameof(UseFixedWidth),
                static o => o.UseFixedWidth,
                static (o, v) => o.UseFixedWidth = v);

        public bool UseFixedWidth
        {
            get => _useFixedWidth;
            set => SetAndRaise(UseFixedWidthProperty, ref _useFixedWidth, value);
        }

        public LauncherTabSizeBox()
        {
            Width = 200;
        }

        protected override Type StyleKeyOverride => typeof(Border);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseFixedWidthProperty)
            {
                if (_useFixedWidth)
                    Width = 200;
                else
                    Width = double.NaN;
            }
        }

        private bool _useFixedWidth = true;
    }

    public partial class LauncherTabBar : LauncherTabBarBase
    {
        public static readonly DirectProperty<LauncherTabBar, bool> IsScrollButtonVisibleProperty =
            AvaloniaProperty.RegisterDirect<LauncherTabBar, bool>(
                nameof(IsScrollButtonVisible),
                static o => o.IsScrollButtonVisible);

        public bool IsScrollButtonVisible
        {
            get => _isScrollButtonVisible;
            set => SetAndRaise(IsScrollButtonVisibleProperty, ref _isScrollButtonVisible, value);
        }

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
            var separatorPen = new Pen(new SolidColorBrush(ActualThemeVariant == ThemeVariant.Dark ? Colors.White : Colors.Black, 0.2));
            var separatorY = (height - 18) * 0.5 + 1;

            if (!_isScrollButtonVisible && selectedIdx > 0)
            {
                var container = LauncherTabsList.ContainerFromIndex(0);
                if (container != null)
                {
                    var x = container.Bounds.Left - startX + LauncherTabsScroller.Bounds.X - 0.5;
                    context.DrawLine(separatorPen, new Point(x, separatorY), new Point(x, separatorY + 18));
                }
            }

            for (var i = 0; i < count; i++)
            {
                if (i == selectedIdx || i == selectedIdx - 1)
                    continue;

                var container = LauncherTabsList.ContainerFromIndex(i);
                if (container == null)
                    continue;

                var containerEndX = container.Bounds.Right;
                if (containerEndX < startX || containerEndX > endX)
                    continue;

                if (_isScrollButtonVisible && i == count - 1)
                    break;

                var separatorX = containerEndX - startX + LauncherTabsScroller.Bounds.X - 0.5;
                context.DrawLine(separatorPen, new Point(separatorX, separatorY), new Point(separatorX, separatorY + 18));
            }

            var selected = LauncherTabsList.ContainerFromIndex(selectedIdx);
            if (selected == null)
                return;

            var activeStartX = selected.Bounds.X;
            var activeEndX = activeStartX + selected.Bounds.Width;
            if (activeStartX > endX + 5 || activeEndX < startX - 5)
                return;

            var geo = new StreamGeometry();
            const double angle = Math.PI / 2;
            var bottom = height + 0.5;
            var cornerSize = new Size(5, 5);

            using (var ctx = geo.Open())
            {
                var drawLeftX = activeStartX - startX + LauncherTabsScroller.Bounds.X;
                if (drawLeftX < LauncherTabsScroller.Bounds.X)
                {
                    ctx.BeginFigure(new Point(LauncherTabsScroller.Bounds.X - 0.5, bottom), true);
                    ctx.LineTo(new Point(LauncherTabsScroller.Bounds.X - 0.5, 0.5));
                }
                else
                {
                    ctx.BeginFigure(new Point(drawLeftX - 5.5, bottom), true);
                    ctx.ArcTo(new Point(drawLeftX - 0.5, bottom - 5), cornerSize, angle, false, SweepDirection.CounterClockwise);
                    ctx.LineTo(new Point(drawLeftX - 0.5, 5.5));
                    ctx.ArcTo(new Point(drawLeftX + 4.5, 0.5), cornerSize, angle, false, SweepDirection.Clockwise);
                }

                var drawRightX = activeEndX - startX + LauncherTabsScroller.Bounds.X;
                if (drawRightX <= LauncherTabsScroller.Bounds.Right)
                {
                    ctx.LineTo(new Point(drawRightX - 5.5, 0.5));
                    ctx.ArcTo(new Point(drawRightX - 0.5, 5.5), cornerSize, angle, false, SweepDirection.Clockwise);
                    ctx.LineTo(new Point(drawRightX - 0.5, bottom - 5));
                    ctx.ArcTo(new Point(drawRightX + 4.5, bottom), cornerSize, angle, false, SweepDirection.CounterClockwise);
                }
                else
                {
                    ctx.LineTo(new Point(LauncherTabsScroller.Bounds.Right - 0.5, 0.5));
                    ctx.LineTo(new Point(LauncherTabsScroller.Bounds.Right - 0.5, bottom));
                }
            }

            var fill = this.FindResource("Brush.ToolBar") as IBrush;
            var stroke = new Pen(this.FindResource("Brush.Border0") as IBrush);
            context.DrawGeometry(fill, stroke, geo);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                InvalidateVisual();
        }

        private void ScrollTabs(object _, PointerWheelEventArgs e)
        {
            if (Math.Abs(e.Delta.X) < Math.Abs(e.Delta.Y))
            {
                var x = LauncherTabsScroller.Offset.X;
                var extent = LauncherTabsScroller.Extent.Width;
                var viewport = LauncherTabsScroller.Viewport.Width;
                var delta = e.Delta.Y;

                if (extent > viewport)
                {
                    x += -delta * 64; // Use the same logic with vertical scrolling in `ScrollContentPresenter`
                    x = Math.Min(Math.Max(x, 0), extent - viewport);
                }

                LauncherTabsScroller.Offset = new Vector(x, 0);
                e.Handled = true;
            }
        }

        private void ScrollTabsLeft(object _, RoutedEventArgs e)
        {
            LauncherTabsScroller.Offset -= _scrollStep;
            e.Handled = true;
        }

        private void ScrollTabsRight(object _, RoutedEventArgs e)
        {
            LauncherTabsScroller.Offset += _scrollStep;
            e.Handled = true;
        }

        private void OnTabsLayoutUpdated(object _1, EventArgs _2)
        {
            IsScrollButtonVisible = LauncherTabsScroller.Extent.Width > LauncherTabsScroller.Viewport.Width;
            InvalidateVisual();
        }

        private void OnTabsSelectionChanged(object _1, SelectionChangedEventArgs _2)
        {
            InvalidateVisual();
        }

        private bool _isScrollButtonVisible = false;
        private readonly Vector _scrollStep = new(64, 0);
    }
}
