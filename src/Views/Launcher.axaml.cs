using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Launcher : ChromelessWindow
    {
        public static readonly StyledProperty<GridLength> CaptionHeightProperty =
            AvaloniaProperty.Register<Launcher, GridLength>(nameof(CaptionHeight));

        public GridLength CaptionHeight
        {
            get => GetValue(CaptionHeightProperty);
            set => SetValue(CaptionHeightProperty, value);
        }

        public static readonly StyledProperty<bool> HasLeftCaptionButtonProperty =
            AvaloniaProperty.Register<Launcher, bool>(nameof(HasLeftCaptionButton));

        public bool HasLeftCaptionButton
        {
            get => GetValue(HasLeftCaptionButtonProperty);
            set => SetValue(HasLeftCaptionButtonProperty, value);
        }

        public bool IsRightCaptionButtonsVisible
        {
            get
            {
                if (OperatingSystem.IsLinux())
                    return !ViewModels.Preference.Instance.UseSystemWindowFrame;
                return OperatingSystem.IsWindows();
            }
        }

        public Launcher()
        {
            var layout = ViewModels.Preference.Instance.Layout;
            if (layout.LauncherWindowState != WindowState.Maximized)
            {
                Width = layout.LauncherWidth;
                Height = layout.LauncherHeight;
            }

            if (OperatingSystem.IsMacOS())
            {
                HasLeftCaptionButton = true;
                CaptionHeight = new GridLength(34);
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome |
                                              ExtendClientAreaChromeHints.OSXThickTitleBar;
            }
            else if (UseSystemWindowFrame)
            {
                CaptionHeight = new GridLength(30);
            }
            else
            {
                CaptionHeight = new GridLength(38);
            }

            InitializeComponent();
        }

        public bool HasKeyModifier(KeyModifiers modifier)
        {
            return _unhandledModifiers.HasFlag(modifier);
        }

        public void ClearKeyModifier()
        {
            _unhandledModifiers = KeyModifiers.None;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var state = ViewModels.Preference.Instance.Layout.LauncherWindowState;
            if (state == WindowState.Maximized || state == WindowState.FullScreen)
                WindowState = WindowState.Maximized;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
            {
                var state = (WindowState)change.NewValue!;
                if (!OperatingSystem.IsMacOS() && !UseSystemWindowFrame)
                    CaptionHeight = new GridLength(state == WindowState.Maximized ? 30 : 38);

                if (OperatingSystem.IsMacOS())
                    HasLeftCaptionButton = state != WindowState.FullScreen;

                ViewModels.Preference.Instance.Layout.LauncherWindowState = state;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var vm = DataContext as ViewModels.Launcher;
            if (vm == null)
                return;

            // We should clear all unhandled key modifiers.
            _unhandledModifiers = KeyModifiers.None;

            // Ctrl+Shift+P opens preference dialog (macOS use hotkeys in system menu bar)
            if (!OperatingSystem.IsMacOS() && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.P)
            {
                App.OpenDialog(new Preference());
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
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

                if (e.Key == Key.N)
                {
                    if (vm.ActivePage.Data is not ViewModels.Welcome)
                        vm.AddNewTab();

                    ViewModels.Welcome.Instance.Clone();
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
                else
                {
                    var welcome = this.FindDescendantOfType<Welcome>();
                    if (welcome != null)
                    {
                        if (e.Key == Key.F)
                        {
                            welcome.SearchBox.Focus();
                            e.Handled = true;
                            return;
                        }
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

            // Record unhandled key modifers.
            if (!e.Handled)
            {
                _unhandledModifiers = e.KeyModifiers;

                if (!_unhandledModifiers.HasFlag(KeyModifiers.Alt) && (e.Key == Key.LeftAlt || e.Key == Key.RightAlt))
                    _unhandledModifiers |= KeyModifiers.Alt;

                if (!_unhandledModifiers.HasFlag(KeyModifiers.Control) && (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl))
                    _unhandledModifiers |= KeyModifiers.Control;

                if (!_unhandledModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.LeftShift || e.Key == Key.RightShift))
                    _unhandledModifiers |= KeyModifiers.Shift;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            _unhandledModifiers = KeyModifiers.None;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            (DataContext as ViewModels.Launcher)?.Quit(Width, Height);
            base.OnClosing(e);
        }

        private void OnOpenWorkspaceMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is ViewModels.Launcher launcher)
            {
                var menu = launcher.CreateContextForWorkspace();
                menu?.Open(btn);
            }

            e.Handled = true;
        }

        private KeyModifiers _unhandledModifiers = KeyModifiers.None;
    }
}
