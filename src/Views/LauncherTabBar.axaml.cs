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
        public static readonly StyledProperty<bool> UseFixedWidthProperty =
            AvaloniaProperty.Register<LauncherTabSizeBox, bool>(nameof(UseFixedWidth), true);

        public bool UseFixedWidth
        {
            get => GetValue(UseFixedWidthProperty);
            set => SetValue(UseFixedWidthProperty, value);
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
                if (UseFixedWidth)
                    Width = 200;
                else
                    Width = double.NaN;
            }
        }
    }

    public partial class LauncherTabBar : UserControl
    {
        public static readonly StyledProperty<bool> IsScrollerVisibleProperty =
            AvaloniaProperty.Register<LauncherTabBar, bool>(nameof(IsScrollerVisible));

        public bool IsScrollerVisible
        {
            get => GetValue(IsScrollerVisibleProperty);
            set => SetValue(IsScrollerVisibleProperty, value);
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

            if (!IsScrollerVisible && selectedIdx > 0)
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

                if (IsScrollerVisible && i == count - 1)
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
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (e.Delta.Y < 0)
                    LauncherTabsScroller.LineRight();
                else if (e.Delta.Y > 0)
                    LauncherTabsScroller.LineLeft();
                e.Handled = true;
            }
        }

        private void ScrollTabsLeft(object _, RoutedEventArgs e)
        {
            LauncherTabsScroller.LineLeft();
            e.Handled = true;
        }

        private void ScrollTabsRight(object _, RoutedEventArgs e)
        {
            LauncherTabsScroller.LineRight();
            e.Handled = true;
        }

        private void OnTabsLayoutUpdated(object _1, EventArgs _2)
        {
            SetCurrentValue(IsScrollerVisibleProperty, LauncherTabsScroller.Extent.Width > LauncherTabsScroller.Viewport.Width);
            InvalidateVisual();
        }

        private void OnTabsSelectionChanged(object _1, SelectionChangedEventArgs _2)
        {
            InvalidateVisual();
        }

        private void OnPointerPressedTab(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border border)
            {
                var point = e.GetCurrentPoint(border);
                if (point.Properties.IsMiddleButtonPressed && border.DataContext is ViewModels.LauncherPage page)
                {
                    (DataContext as ViewModels.Launcher)?.CloseTab(page);
                    e.Handled = true;
                }
                else
                {
                    _pressedTab = true;
                    _startDragTab = false;
                    _pressedTabPosition = e.GetPosition(border);
                }
            }
        }

        private void OnPointerReleasedTab(object _1, PointerReleasedEventArgs _2)
        {
            _pressedTab = false;
            _startDragTab = false;
        }

        private async void OnPointerMovedOverTab(object sender, PointerEventArgs e)
        {
            if (_pressedTab && !_startDragTab && sender is Border { DataContext: ViewModels.LauncherPage page } border)
            {
                var delta = e.GetPosition(border) - _pressedTabPosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTab = true;

                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(_dndMainTabFormat, page.Node.Id));
                await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
            }
            e.Handled = true;
        }

        private void DropTab(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.TryGetValue(_dndMainTabFormat) is not { Length: > 0 } id)
                return;

            if (DataContext is not ViewModels.Launcher launcher)
                return;

            ViewModels.LauncherPage target = null;
            foreach (var page in launcher.Pages)
            {
                if (page.Node.Id.Equals(id, StringComparison.Ordinal))
                {
                    target = page;
                    break;
                }
            }

            if (target == null)
                return;

            if (sender is not Border { DataContext: ViewModels.LauncherPage to })
                return;

            if (target == to)
                return;

            launcher.MoveTab(target, to);

            _pressedTab = false;
            _startDragTab = false;
            e.Handled = true;
        }

        private void OnTabContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Border { DataContext: ViewModels.LauncherPage page } border &&
                DataContext is ViewModels.Launcher vm)
            {
                var menu = new ContextMenu();

                if (vm.ActivePage.Data is ViewModels.Repository repo)
                {
                    var refresh = new MenuItem();
                    refresh.Header = App.Text("PageTabBar.Tab.Refresh");
                    refresh.Icon = App.CreateMenuIcon("Icons.Loading");
                    refresh.Tag = "F5";
                    refresh.Click += (_, ev) =>
                    {
                        repo.RefreshAll();
                        ev.Handled = true;
                    };
                    menu.Items.Add(refresh);

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("PageTabBar.Tab.CopyPath");
                    copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyPath.Click += async (_, ev) =>
                    {
                        await page.CopyPathAsync();
                        ev.Handled = true;
                    };
                    menu.Items.Add(copyPath);
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var bookmark = new MenuItem();
                    bookmark.Header = App.Text("PageTabBar.Tab.Bookmark");
                    bookmark.Icon = App.CreateMenuIcon("Icons.Bookmark");

                    for (int i = 0; i < Models.Bookmarks.Brushes.Length; i++)
                    {
                        var brush = Models.Bookmarks.Brushes[i];
                        var icon = App.CreateMenuIcon("Icons.Bookmark");
                        if (brush != null)
                            icon.Fill = brush;

                        var dupIdx = i;
                        var setter = new MenuItem();
                        setter.Header = icon;
                        setter.Click += (_, ev) =>
                        {
                            page.Node.Bookmark = dupIdx;
                            ev.Handled = true;
                        };
                        bookmark.Items.Add(setter);
                    }
                    menu.Items.Add(bookmark);

                    var workspaces = ViewModels.Preferences.Instance.Workspaces;
                    if (workspaces.Count > 1)
                    {
                        var moveTo = new MenuItem();
                        moveTo.Header = App.Text("PageTabBar.Tab.MoveToWorkspace");
                        moveTo.Icon = App.CreateMenuIcon("Icons.MoveTo");

                        foreach (var ws in workspaces)
                        {
                            var dupWs = ws;
                            var isCurrent = dupWs == vm.ActiveWorkspace;
                            var icon = App.CreateMenuIcon(isCurrent ? "Icons.Check" : "Icons.Workspace");
                            icon.Fill = dupWs.Brush;

                            var target = new MenuItem();
                            target.Header = ws.Name;
                            target.Icon = icon;
                            target.Click += (_, ev) =>
                            {
                                if (!isCurrent)
                                {
                                    vm.CloseTab(page);
                                    dupWs.Repositories.Add(repo.FullPath);
                                }

                                ev.Handled = true;
                            };
                            moveTo.Items.Add(target);
                        }

                        menu.Items.Add(moveTo);
                    }

                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var close = new MenuItem();
                close.Header = App.Text("PageTabBar.Tab.Close");
                close.Tag = OperatingSystem.IsMacOS() ? "⌘+W" : "Ctrl+W";
                close.Click += (_, ev) =>
                {
                    vm.CloseTab(page);
                    ev.Handled = true;
                };
                menu.Items.Add(close);

                var closeOthers = new MenuItem();
                closeOthers.Header = App.Text("PageTabBar.Tab.CloseOther");
                closeOthers.Click += (_, ev) =>
                {
                    vm.CloseOtherTabs();
                    ev.Handled = true;
                };
                menu.Items.Add(closeOthers);

                var closeRight = new MenuItem();
                closeRight.Header = App.Text("PageTabBar.Tab.CloseRight");
                closeRight.Click += (_, ev) =>
                {
                    vm.CloseRightTabs();
                    ev.Handled = true;
                };
                menu.Items.Add(closeRight);
                menu.Open(border);
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
        private Point _pressedTabPosition = new();
        private bool _startDragTab = false;
        private readonly DataFormat<string> _dndMainTabFormat = DataFormat.CreateStringApplicationFormat("sourcegit-dnd-main-tab");
    }
}
