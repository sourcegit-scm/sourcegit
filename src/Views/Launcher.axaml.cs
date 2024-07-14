using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Launcher : ChromelessWindow
    {
        public Launcher()
        {
            var layout = ViewModels.Preference.Instance.Layout;
            WindowState = layout.LauncherWindowState;

            if (WindowState != WindowState.Maximized)
            {
                Width = layout.LauncherWidth;
                Height = layout.LauncherHeight;
            }

            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty && MainLayout != null)
            {
                var state = (WindowState)change.NewValue!;
                if (state == WindowState.Maximized)
                    MainLayout.RowDefinitions[0].Height = new GridLength(OperatingSystem.IsMacOS() ? 34 : 30);
                else
                    MainLayout.RowDefinitions[0].Height = new GridLength(38);

                ViewModels.Preference.Instance.Layout.LauncherWindowState = state;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var vm = DataContext as ViewModels.Launcher;
            if (vm == null)
                return;

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

                if (e.Key == Key.T)
                {
                    vm.AddNewTab();
                    e.Handled = true;
                    return;
                }

                if ((OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Right) ||
                    (!OperatingSystem.IsMacOS() && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
                {
                    vm.GotoNextTab();
                    e.Handled = true;
                    return;
                }

                if ((OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Left) ||
                    (!OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
                {
                    vm.GotoPrevTab();
                    e.Handled = true;
                    return;
                }

                if (vm.ActivePage.Data is ViewModels.Repository repo)
                {
                    if (e.Key == Key.D1 || e.Key == Key.NumPad1)
                    {
                        repo.SelectedViewIndex = 0;
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.D2 || e.Key == Key.NumPad2)
                    {
                        repo.SelectedViewIndex = 1;
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.D3 || e.Key == Key.NumPad3)
                    {
                        repo.SelectedViewIndex = 2;
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.F)
                    {
                        repo.IsSearching = true;
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.H && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        repo.IsSearching = false;
                        e.Handled = true;
                        return;
                    }
                }
            }
            else if (e.Key == Key.Escape)
            {
                vm.ActivePage.CancelPopup();
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
            var pref = ViewModels.Preference.Instance;
            pref.Layout.LauncherWidth = Width;
            pref.Layout.LauncherHeight = Height;

            var vm = DataContext as ViewModels.Launcher;
            vm?.Quit();

            base.OnClosing(e);
        }

        private void OnTitleBarDoubleTapped(object _, TappedEventArgs e)
        {
            _pressedTitleBar = false;

            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            if (e.ClickCount != 2)
                _pressedTitleBar = true;
        }

        private void MoveWindow(object _, PointerEventArgs e)
        {
            if (!_pressedTitleBar || e.Source == null)
                return;

            var visual = (Visual)e.Source;
            if (visual == null)
                return;

#pragma warning disable CS0618
            BeginMoveDrag(new PointerPressedEventArgs(
                e.Source,
                e.Pointer,
                visual,
                e.GetPosition(visual),
                e.Timestamp,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                e.KeyModifiers));
#pragma warning restore CS0618
        }

        private void EndMoveWindow(object _1, PointerReleasedEventArgs _2)
        {
            _pressedTitleBar = false;
        }

        private void OnPopupSure(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher vm)
                vm.ActivePage.ProcessPopup();

            e.Handled = true;
        }

        private void OnPopupCancel(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher vm)
                vm.ActivePage.CancelPopup();

            e.Handled = true;
        }

        private void OnPopupCancelByClickMask(object sender, PointerPressedEventArgs e)
        {
            OnPopupCancel(sender, e);
        }

        private void OnDismissNotification(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is ViewModels.Launcher vm)
                vm.DismissNotification(btn.DataContext as ViewModels.Notification);

            e.Handled = true;
        }

        private bool _pressedTitleBar = false;
    }
}
