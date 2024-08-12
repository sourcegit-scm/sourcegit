using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class Launcher : ChromelessWindow
    {
        public Launcher()
        {
            var layout = ViewModels.Preference.Instance.Layout;
            if (layout.LauncherWindowState != WindowState.Maximized)
            {
                Width = layout.LauncherWidth;
                Height = layout.LauncherHeight;
            }

            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var layout = ViewModels.Preference.Instance.Layout;
            if (layout.LauncherWindowState == WindowState.Maximized)
                WindowState = WindowState.Maximized;
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
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 1)
                BeginMoveDrag(e);

            e.Handled = true;
        }
    }
}
