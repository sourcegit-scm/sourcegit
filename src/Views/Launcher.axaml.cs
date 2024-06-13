using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Launcher : ChromelessWindow, Models.INotificationReceiver
    {
        public static readonly StyledProperty<GridLength> TitleBarHeightProperty =
            AvaloniaProperty.Register<Launcher, GridLength>(nameof(TitleBarHeight), new GridLength(38, GridUnitType.Pixel));

        public GridLength TitleBarHeight
        {
            get => GetValue(TitleBarHeightProperty);
            set => SetValue(TitleBarHeightProperty, value);
        }

        public Launcher()
        {
            DataContext = new ViewModels.Launcher();
            InitializeComponent();
        }

        public void OnReceiveNotification(string ctx, Models.Notification notice)
        {
            if (DataContext is ViewModels.Launcher vm)
            {
                foreach (var page in vm.Pages)
                {
                    var pageId = page.Node.Id.Replace("\\", "/");
                    if (pageId == ctx)
                    {
                        page.Notifications.Add(notice);
                        return;
                    }
                }

                if (vm.ActivePage != null)
                    vm.ActivePage.Notifications.Add(notice);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
            {
                var state = (WindowState)change.NewValue;
                if (state == WindowState.Maximized)
                    SetCurrentValue(TitleBarHeightProperty, new GridLength(OperatingSystem.IsMacOS() ? 34 : 30));
                else
                    SetCurrentValue(TitleBarHeightProperty, new GridLength(38, GridUnitType.Pixel));
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var vm = DataContext as ViewModels.Launcher;

            // Ctrl+Shift+P opens preference dialog (macOS use hotkeys in system menu bar)
            if (!OperatingSystem.IsMacOS() && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.P)
            {
                App.OpenPreferenceCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if ((OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Meta)) ||
                (!OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
            {
                if (e.Key == Key.W)
                {
                    vm.CloseTab(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.T)
                {
                    vm.AddNewTab();
                    e.Handled = true;
                    return;
                }
                else if ((OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Right) ||
                    (!OperatingSystem.IsMacOS() && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
                {
                    vm.GotoNextTab();
                    e.Handled = true;
                    return;
                }
                else if ((OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Left) ||
                    (!OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
                {
                    vm.GotoPrevTab();
                    e.Handled = true;
                    return;
                }
                else if (vm.ActivePage.Data is ViewModels.Repository repo)
                {
                    if (e.Key == Key.D1 || e.Key == Key.NumPad1)
                    {
                        repo.SelectedViewIndex = 0;
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.D2 || e.Key == Key.NumPad2)
                    {
                        repo.SelectedViewIndex = 1;
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.D3 || e.Key == Key.NumPad3)
                    {
                        repo.SelectedViewIndex = 2;
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.F)
                    {
                        repo.IsSearching = true;
                        e.Handled = true;
                        return;
                    }
                }
            }
            else if (e.Key == Key.Escape)
            {
                vm.ActivePage.CancelPopup();

                if (vm.ActivePage.Data is ViewModels.Repository repo)
                {
                    repo.IsSearching = false;
                }

                e.Handled = true;
                return;
            }
            else if (e.Key == Key.F5)
            {
                if (vm.ActivePage.Data is ViewModels.Repository repo)
                {
                    repo.RefreshAll();
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            var vm = DataContext as ViewModels.Launcher;
            vm.Quit();

            base.OnClosing(e);
        }

        private void OnTitleBarDoubleTapped(object sender, TappedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount != 2)
                BeginMoveDrag(e);
        }

        private void ScrollTabs(object sender, PointerWheelEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (e.Delta.Y < 0)
                    launcherTabsScroller.LineRight();
                else if (e.Delta.Y > 0)
                    launcherTabsScroller.LineLeft();
                e.Handled = true;
            }
        }

        private void ScrollTabsLeft(object sender, RoutedEventArgs e)
        {
            launcherTabsScroller.LineLeft();
            e.Handled = true;
        }

        private void ScrollTabsRight(object sender, RoutedEventArgs e)
        {
            launcherTabsScroller.LineRight();
            e.Handled = true;
        }

        private void UpdateScrollIndicator(object sender, SizeChangedEventArgs e)
        {
            if (launcherTabsBar.Bounds.Width > launcherTabsContainer.Bounds.Width)
            {
                leftScrollIndicator.IsVisible = true;
                rightScrollIndicator.IsVisible = true;
            }
            else
            {
                leftScrollIndicator.IsVisible = false;
                rightScrollIndicator.IsVisible = false;
            }
            e.Handled = true;
        }

        private void OnTabsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                leftScrollIndicator.IsEnabled = scrollViewer.Offset.X > 0;
                rightScrollIndicator.IsEnabled = scrollViewer.Offset.X < scrollViewer.Extent.Width - scrollViewer.Viewport.Width;
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
            _pressedTab = true;
            _startDrag = false;
            _pressedTabPosition = e.GetPosition(sender as Border);
        }

        private void OnPointerReleasedTab(object sender, PointerReleasedEventArgs e)
        {
            _pressedTab = false;
            _startDrag = false;
        }

        private void OnPointerMovedOverTab(object sender, PointerEventArgs e)
        {
            if (_pressedTab && !_startDrag && sender is Border border)
            {
                var delta = e.GetPosition(border) - _pressedTabPosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDrag = true;

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
            _startDrag = false;
            e.Handled = true;
        }

        private void OnPopupSure(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher vm)
            {
                vm.ActivePage.ProcessPopup();
            }
            e.Handled = true;
        }

        private void OnPopupCancel(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher vm)
            {
                vm.ActivePage.CancelPopup();
            }
            e.Handled = true;
        }

        private void OnPopupCancelByClickMask(object sender, PointerPressedEventArgs e)
        {
            OnPopupCancel(sender, e);
        }

        private bool _pressedTab = false;
        private Point _pressedTabPosition = new Point();
        private bool _startDrag = false;
    }
}
