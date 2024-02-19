using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace SourceGit {
    public partial class App : Application {

        [STAThread]
        public static void Main(string[] args) {
            try {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            } catch (Exception ex) {
                var builder = new StringBuilder();
                builder.Append("Crash: ");
                builder.Append(ex.Message);
                builder.Append("\n\n");
                builder.Append("----------------------------\n");
                builder.Append($"Version: {Assembly.GetExecutingAssembly().GetName().Version}\n");
                builder.Append($"OS: {Environment.OSVersion.ToString()}\n");
                builder.Append($"Framework: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}\n");
                builder.Append($"Source: {ex.Source}\n");
                builder.Append($"---------------------------\n\n");
                builder.Append(ex.StackTrace);

                var time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SourceGit",
                    $"crash_{time}.log");
                File.WriteAllText(file, builder.ToString());
            } 
        }

        public static AppBuilder BuildAvaloniaApp() {
            var builder = AppBuilder.Configure<App>();
            builder.UsePlatformDetect();

            if (OperatingSystem.IsWindows()) {
                builder.With(new FontManagerOptions() {
                    FontFallbacks = [
                        new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") }
                    ]
                });
            } else if (OperatingSystem.IsMacOS()) {
                builder.With(new FontManagerOptions() {
                    FontFallbacks = [
                        new FontFallback { FontFamily = new FontFamily("PingFang SC") }
                    ]
                });
                builder.With(new MacOSPlatformOptions() {
                    DisableDefaultApplicationMenuItems = true,
                    DisableNativeMenus = true,
                });
            }
            
            builder.LogToTrace();
            return builder;
        }

        public static void RaiseException(string context, string message) {
            if (Current is App app && app._notificationReceiver != null) {
                var ctx = context.Replace('\\', '/');
                var notice = new Models.Notification() { IsError = true, Message = message };
                app._notificationReceiver.OnReceiveNotification(ctx, notice);
            }
        }

        public static void SendNotification(string context, string message) {
            if (Current is App app && app._notificationReceiver != null) {
                var ctx = context.Replace('\\', '/');
                var notice = new Models.Notification() { IsError = false, Message = message };
                app._notificationReceiver.OnReceiveNotification(ctx, notice);
            }
        }

        public static void SetLocale(string localeKey) {
            var app = Current as App;
            var targetLocale = app.Resources[localeKey] as ResourceDictionary;
            if (targetLocale == null || targetLocale == app._activeLocale) {
                return;
            }

            if (app._activeLocale != null) {
                app.Resources.MergedDictionaries.Remove(app._activeLocale);
            }

            app.Resources.MergedDictionaries.Add(targetLocale);
            app._activeLocale = targetLocale;
        }

        public static void SetTheme(string theme) {
            if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase)) {
                App.Current.RequestedThemeVariant = ThemeVariant.Light;
            } else if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)) {
                App.Current.RequestedThemeVariant = ThemeVariant.Dark;
            } else {
                App.Current.RequestedThemeVariant = ThemeVariant.Default;
            }
        }

        public static async void CopyText(string data) {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                if (desktop.MainWindow.Clipboard is { } clipbord) {
                    await clipbord.SetTextAsync(data);
                }
            }
        }

        public static string Text(string key, params object[] args) {
            var fmt = Current.FindResource($"Text.{key}") as string;
            if (string.IsNullOrWhiteSpace(fmt)) return $"Text.{key}";
            return string.Format(fmt, args);
        }

        public static TopLevel GetTopLevel() {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                return desktop.MainWindow;
            }
            return null;
        }

        public static void Quit() {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow.Close();
                desktop.Shutdown();
            }
        }

        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);

            SetLocale(ViewModels.Preference.Instance.Locale);
            SetTheme(ViewModels.Preference.Instance.Theme);
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                BindingPlugins.DataValidators.RemoveAt(0);

                var launcher = new Views.Launcher();
                _notificationReceiver = launcher;
                desktop.MainWindow = launcher;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private ResourceDictionary _activeLocale = null;
        private Models.INotificationReceiver _notificationReceiver = null;
    }
}