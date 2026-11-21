using System;
using System.IO;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

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

    public partial class LauncherTabBar : UserControl
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
                else if (point.Properties.IsLeftButtonPressed)
                {
                    _pressedTabEvent = e;
                    _startDragTab = false;
                }
                else
                {
                    _pressedTabEvent = null;
                    _startDragTab = false;
                }
            }
        }

        private void OnPointerReleasedTab(object _1, PointerReleasedEventArgs _2)
        {
            _pressedTabEvent = null;
            _startDragTab = false;
        }

        private async void OnPointerMovedOverTab(object sender, PointerEventArgs e)
        {
            if (_pressedTabEvent != null && !_startDragTab && sender is Border { DataContext: ViewModels.LauncherPage page } border)
            {
                var delta = e.GetPosition(border) - _pressedTabEvent.GetPosition(border);
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTab = true;

                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(_dndMainTabFormat, page.Node.Id));
                try
                {
                    BeginDragFeedback(border, page);
                    await DragDrop.DoDragDropAsync(_pressedTabEvent, data, DragDropEffects.Move);
                }
                finally
                {
                    ClearDragFeedback();
                    _pressedTabEvent = null;
                    _startDragTab = false;
                }
            }
            e.Handled = true;
        }

        private void BeginDragFeedback(Border source, ViewModels.LauncherPage page)
        {
            _draggedPage = page;
            _dragSource = source;
            _dragLayer = AdornerLayer.GetAdornerLayer(this);
            if (_dragLayer != null)
            {
                var size = source.Bounds.Size;
                var text = source.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.IsEffectivelyVisible);
                var icon = page.Node.IsRepository
                    ? (Control)new Bookmark { Value = page.Node.Bookmark }
                    : new Avalonia.Controls.Shapes.Path
                    {
                        Data = this.FindResource("Icons.Repositories") as Geometry,
                        Fill = this.FindResource("Brush.FG1") as IBrush
                    };
                icon.Width = 12;
                icon.Height = 12;
                icon.Margin = new Thickness(0, 0, 8, 0);
                var content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    Margin = new Thickness(12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                content.Children.Add(icon);
                var title = new TextBlock
                {
                    Text = page.Node.IsRepository ? page.Node.Name : App.Text("PageTabBar.Welcome.Title"),
                    FontFamily = text?.FontFamily ?? FontFamily.Default,
                    FontSize = text?.FontSize ?? 12,
                    Foreground = this.FindResource("Brush.FG1") as IBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(title, 1);
                content.Children.Add(title);
                _dragPreview = new Border
                {
                    Width = size.Width,
                    Height = size.Height,
                    Background = this.FindResource("Brush.ToolBar") as IBrush,
                    BorderBrush = this.FindResource("Brush.Border0") as IBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Opacity = 0.85,
                    IsVisible = false,
                    ClipToBounds = true,
                    Child = content
                };
                _dropIndicator = new Border
                {
                    Width = 3,
                    Height = Math.Max(0, size.Height - 4),
                    Background = this.FindResource("Brush.Accent") as IBrush,
                    IsVisible = false
                };
                _dragOverlay = new Canvas { IsHitTestVisible = false };
                _dragOverlay.Children.Add(_dropIndicator);
                _dragOverlay.Children.Add(_dragPreview);
                AdornerLayer.SetAdornedElement(_dragOverlay, this);
                AdornerLayer.SetIsClipEnabled(_dragOverlay, false);
                _dragLayer.Children.Add(_dragOverlay);
            }
            source.Classes.Add("tab_drag_source");
        }

        private void DragOverTabs(object sender, DragEventArgs e)
        {
            if (_draggedPage == null || DataContext is not ViewModels.Launcher launcher)
                return;

            var point = e.GetPosition(this);
            if (_dragPreview != null)
            {
                Canvas.SetLeft(_dragPreview, point.X + 12);
                Canvas.SetTop(_dragPreview, point.Y + 12);
                _dragPreview.IsVisible = true;
            }

            ClearDropTarget();
            e.DragEffects = DragDropEffects.None;
            var fromIndex = launcher.Pages.IndexOf(_draggedPage);
            if (fromIndex < 0 || !new Rect(LauncherTabsScroller.Bounds.Size).Contains(e.GetPosition(LauncherTabsScroller)))
                return;

            for (var i = 0; i < LauncherTabsList.ItemCount; i++)
            {
                var container = LauncherTabsList.ContainerFromIndex(i);
                var tab = container?.GetVisualDescendants().OfType<LauncherTabSizeBox>().FirstOrDefault();
                if (tab == null || i == fromIndex)
                    continue;

                var position = e.GetPosition(tab);
                if (!new Rect(tab.Bounds.Size).Contains(position))
                    continue;

                _dropTarget = tab;
                tab.Classes.Add("tab_drop_target");
                e.DragEffects = DragDropEffects.Move;
                if (_dropIndicator != null && tab.TranslatePoint(new Point(i > fromIndex ? tab.Bounds.Width : 0, 2), this) is { } edge)
                {
                    Canvas.SetLeft(_dropIndicator, edge.X - 1.5);
                    Canvas.SetTop(_dropIndicator, edge.Y);
                    _dropIndicator.IsVisible = true;
                }
                break;
            }
            e.Handled = true;
        }

        private void DragLeaveTabs(object sender, DragEventArgs e)
        {
            ClearDropTarget();
            if (_dragPreview != null)
                _dragPreview.IsVisible = false;
        }

        private void ClearDropTarget()
        {
            _dropTarget?.Classes.Remove("tab_drop_target");
            _dropTarget = null;
            if (_dropIndicator != null)
                _dropIndicator.IsVisible = false;
        }

        private void ClearDragFeedback()
        {
            ClearDropTarget();
            _dragSource?.Classes.Remove("tab_drag_source");
            if (_dragOverlay != null)
                _dragLayer?.Children.Remove(_dragOverlay);
            _draggedPage = null;
            _dragSource = null;
            _dragOverlay = null;
            _dragPreview = null;
            _dropIndicator = null;
            _dragLayer = null;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ClearDragFeedback();
            base.OnDetachedFromVisualTree(e);
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

            ClearDragFeedback();
            launcher.MoveTab(target, to);

            _pressedTabEvent = null;
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
                    var explore = new MenuItem();
                    explore.Header = App.Text("Repository.Explore");
                    explore.Icon = this.CreateMenuIcon("Icons.Explore");
                    explore.Tag = OperatingSystem.IsMacOS() ? "⌘+E" : "Ctrl+E";
                    explore.Click += (_, e) =>
                    {
                        Native.OS.OpenInFileManager(repo.FullPath);
                        e.Handled = true;
                    };
                    menu.Items.Add(explore);

                    var refresh = new MenuItem();
                    refresh.Header = App.Text("PageTabBar.Tab.Refresh");
                    refresh.Icon = this.CreateMenuIcon("Icons.Loading");
                    refresh.Tag = "F5";
                    refresh.Click += (_, ev) =>
                    {
                        repo.RefreshAll();
                        ev.Handled = true;
                    };
                    menu.Items.Add(refresh);

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("PageTabBar.Tab.CopyPath");
                    copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                    copyPath.Click += async (_, ev) =>
                    {
                        var dir = new DirectoryInfo(repo.FullPath);
                        await this.CopyTextAsync(dir.FullName);
                        ev.Handled = true;
                    };
                    menu.Items.Add(copyPath);
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var edit = new MenuItem();
                    edit.Header = App.Text("PageTabBar.Tab.Edit");
                    edit.Icon = this.CreateMenuIcon("Icons.Edit");
                    edit.Click += (_, ev) =>
                    {
                        page.Node.Edit();
                        ev.Handled = true;
                    };
                    menu.Items.Add(edit);

                    var workspaces = ViewModels.Preferences.Instance.Workspaces;
                    if (workspaces.Count > 1)
                    {
                        var moveTo = new MenuItem();
                        moveTo.Header = App.Text("PageTabBar.Tab.MoveToWorkspace");
                        moveTo.Icon = this.CreateMenuIcon("Icons.MoveTo");

                        foreach (var ws in workspaces)
                        {
                            var dupWs = ws;
                            var isCurrent = dupWs == vm.ActiveWorkspace;
                            var icon = this.CreateMenuIcon(isCurrent ? "Icons.Check" : "Icons.Workspace");
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

        private bool _isScrollButtonVisible = false;
        private readonly Vector _scrollStep = new(64, 0);
        private PointerPressedEventArgs _pressedTabEvent = null;
        private bool _startDragTab = false;
        private ViewModels.LauncherPage _draggedPage;
        private Border _dragSource;
        private LauncherTabSizeBox _dropTarget;
        private AdornerLayer _dragLayer;
        private Canvas _dragOverlay;
        private Border _dragPreview;
        private Border _dropIndicator;
        private readonly DataFormat<string> _dndMainTabFormat = DataFormat.CreateStringApplicationFormat("sourcegit-dnd-main-tab");
    }
}
