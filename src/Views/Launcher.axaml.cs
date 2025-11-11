using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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

        public bool HasRightCaptionButton
        {
            get
            {
                if (OperatingSystem.IsLinux())
                    return !Native.OS.UseSystemWindowFrame;

                return OperatingSystem.IsWindows();
            }
        }

        public Launcher()
        {
            if (OperatingSystem.IsMacOS())
            {
                HasLeftCaptionButton = true;
                CaptionHeight = new GridLength(34);
                ExtendClientAreaChromeHints |= ExtendClientAreaChromeHints.OSXThickTitleBar;
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
            PositionChanged += OnPositionChanged;

            var layout = ViewModels.Preferences.Instance.Layout;
            Width = layout.LauncherWidth;
            Height = layout.LauncherHeight;

            var x = layout.LauncherPositionX;
            var y = layout.LauncherPositionY;
            if (x != int.MinValue && y != int.MinValue && Screens is { } screens)
            {
                var position = new PixelPoint(x, y);
                var size = new PixelSize((int)layout.LauncherWidth, (int)layout.LauncherHeight);
                var desiredRect = new PixelRect(position, size);
                for (var i = 0; i < screens.ScreenCount; i++)
                {
                    var screen = screens.All[i];
                    if (screen.WorkingArea.Contains(desiredRect))
                    {
                        Position = position;
                        return;
                    }
                }
            }

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public void BringToTop()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = _lastWindowState;
            else
                Activate();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var state = ViewModels.Preferences.Instance.Layout.LauncherWindowState;
            if (state == WindowState.Maximized || state == WindowState.FullScreen)
                WindowState = WindowState.Maximized;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
            {
                var state = (WindowState)change.NewValue!;
                _lastWindowState = (WindowState)change.OldValue!;

                if (OperatingSystem.IsMacOS())
                    HasLeftCaptionButton = state != WindowState.FullScreen;
                else if (!UseSystemWindowFrame)
                    CaptionHeight = new GridLength(state == WindowState.Maximized ? 30 : 38);

                ViewModels.Preferences.Instance.Layout.LauncherWindowState = state;
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            if (WindowState == WindowState.Normal)
            {
                var layout = ViewModels.Preferences.Instance.Layout;
                layout.LauncherWidth = Width;
                layout.LauncherHeight = Height;
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher vm)
                return;

            // Check for AltGr (which is detected as Ctrl+Alt)
            bool isAltGr = e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                           e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            // Skip hotkey processing if AltGr is pressed
            if (isAltGr)
            {
                base.OnKeyDown(e);
                return;
            }

            // Register hotkeys for Windows/Linux (macOS has registered these keys in system menu bar)
            if (!OperatingSystem.IsMacOS())
            {
                if (e is { KeyModifiers: KeyModifiers.Control, Key: Key.OemComma })
                {
                    await App.ShowDialog(new Preferences());
                    e.Handled = true;
                    return;
                }

                if (e is { KeyModifiers: KeyModifiers.None, Key: Key.F1 })
                {
                    await App.ShowDialog(new Hotkeys());
                    return;
                }

                if (e is { KeyModifiers: KeyModifiers.Control, Key: Key.Q })
                {
                    App.Quit(0);
                    return;
                }
            }

            if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                if (e.Key == Key.W)
                {
                    vm.CloseTab(null);
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
                    switch (e.Key)
                    {
                        case Key.D1 or Key.NumPad1:
                            repo.SelectedViewIndex = 0;
                            e.Handled = true;
                            return;
                        case Key.D2 or Key.NumPad2:
                            repo.SelectedViewIndex = 1;
                            e.Handled = true;
                            return;
                        case Key.D3 or Key.NumPad3:
                            repo.SelectedViewIndex = 2;
                            e.Handled = true;
                            return;
                        case Key.F:
                            repo.IsSearchingCommits = true;
                            e.Handled = true;
                            return;
                        case Key.H when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                            repo.IsSearchingCommits = false;
                            e.Handled = true;
                            return;
                        case Key.P when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                            vm.OpenCommandPalette(new ViewModels.RepositoryCommandPalette(vm, repo));
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
                if (vm.CommandPalette != null)
                    vm.CancelCommandPalette();
                else
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
                else if (vm.ActivePage.Data is ViewModels.Welcome welcome)
                {
                    e.Handled = true;
                    await welcome.UpdateStatusAsync(true, null);
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (!Design.IsDesignMode && DataContext is ViewModels.Launcher launcher)
            {
                ViewModels.Preferences.Instance.Save();
                launcher.Quit();
            }
        }

        private void OnPositionChanged(object sender, PixelPointEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                var layout = ViewModels.Preferences.Instance.Layout;
                layout.LauncherPositionX = Position.X;
                layout.LauncherPositionY = Position.Y;
            }
        }

        private void OnOpenWorkspaceMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is ViewModels.Launcher launcher)
            {
                var pref = ViewModels.Preferences.Instance;
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.VerticalOffset = -6;

                var groupHeader = new TextBlock()
                {
                    Text = App.Text("Launcher.Workspaces"),
                    FontWeight = FontWeight.Bold,
                };

                var workspaces = new MenuItem();
                workspaces.Header = groupHeader;
                workspaces.IsEnabled = false;
                menu.Items.Add(workspaces);

                for (var i = 0; i < pref.Workspaces.Count; i++)
                {
                    var workspace = pref.Workspaces[i];

                    var icon = App.CreateMenuIcon(workspace.IsActive ? "Icons.Check" : "Icons.Workspace");
                    icon.Fill = workspace.Brush;

                    var item = new MenuItem();
                    item.Header = workspace.Name;
                    item.Icon = icon;
                    item.Click += (_, ev) =>
                    {
                        if (!workspace.IsActive)
                            launcher.SwitchWorkspace(workspace);

                        ev.Handled = true;
                    };

                    menu.Items.Add(item);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });

                var configure = new MenuItem();
                configure.Header = App.Text("Workspace.Configure");
                configure.Click += async (_, ev) =>
                {
                    await App.ShowDialog(new ViewModels.ConfigureWorkspace());
                    ev.Handled = true;
                };
                menu.Items.Add(configure);
                menu.Open(btn);
            }

            e.Handled = true;
        }

        private void OnOpenPagesCommandPalette(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher launcher)
                launcher.OpenCommandPalette(new ViewModels.LauncherPagesCommandPalette(launcher));
            e.Handled = true;
        }

        private void OnCloseCommandPalette(object sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender && DataContext is ViewModels.Launcher launcher)
                launcher.CancelCommandPalette();
            e.Handled = true;
        }

        private WindowState _lastWindowState = WindowState.Normal;
    }
}
