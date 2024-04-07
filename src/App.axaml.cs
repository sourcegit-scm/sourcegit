using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Styling;
using Avalonia.Threading;

namespace SourceGit
{
    public partial class App : Application
    {

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
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

        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>();
            builder.UsePlatformDetect();
            builder.LogToTrace();
            builder.ConfigureFonts(manager =>
            {
                var monospace = new EmbeddedFontCollection(
                    new Uri("fonts:SourceGit", UriKind.Absolute),
                    new Uri("avares://SourceGit/Resources/Fonts", UriKind.Absolute));
                manager.AddFontCollection(monospace);
            });

            Native.OS.SetupApp(builder);
            return builder;
        }

        public static void RaiseException(string context, string message)
        {
            if (Current is App app && app._notificationReceiver != null)
            {
                var notice = new Models.Notification() { IsError = true, Message = message };
                app._notificationReceiver.OnReceiveNotification(context, notice);
            }
        }

        public static void SendNotification(string context, string message)
        {
            if (Current is App app && app._notificationReceiver != null)
            {
                var notice = new Models.Notification() { IsError = false, Message = message };
                app._notificationReceiver.OnReceiveNotification(context, notice);
            }
        }

        public static void SetLocale(string localeKey)
        {
            var app = Current as App;
            var targetLocale = app.Resources[localeKey] as ResourceDictionary;
            if (targetLocale == null || targetLocale == app._activeLocale)
            {
                return;
            }

            if (app._activeLocale != null)
            {
                app.Resources.MergedDictionaries.Remove(app._activeLocale);
            }

            app.Resources.MergedDictionaries.Add(targetLocale);
            app._activeLocale = targetLocale;
        }

        public static void SetTheme(string theme)
        {
            if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
            else
            {
                Current.RequestedThemeVariant = ThemeVariant.Default;
            }
        }

        public static async void CopyText(string data)
        {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow.Clipboard is { } clipbord)
                {
                    await clipbord.SetTextAsync(data);
                }
            }
        }

        public static string Text(string key, params object[] args)
        {
            var fmt = Current.FindResource($"Text.{key}") as string;
            if (string.IsNullOrWhiteSpace(fmt))
                return $"Text.{key}";
            return string.Format(fmt, args);
        }

        public static Avalonia.Controls.Shapes.Path CreateMenuIcon(string key)
        {
            var icon = new Avalonia.Controls.Shapes.Path();
            icon.Width = 12;
            icon.Height = 12;
            icon.Stretch = Stretch.Uniform;
            icon.Data = Current.FindResource(key) as StreamGeometry;
            return icon;
        }

        public static TopLevel GetTopLevel()
        {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        public static void Check4Update(bool manually = false)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Fetch lastest release information.
                    var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(2) };
                    var data = await client.GetStringAsync("https://api.github.com/repos/sourcegit-scm/sourcegit/releases/latest");

                    // Parse json into Models.Version.
                    var ver = JsonSerializer.Deserialize(data, JsonCodeGen.Default.Version);
                    if (ver == null)
                        return;

                    // Check if already up-to-date.
                    if (!ver.IsNewVersion)
                    {
                        if (manually)
                            ShowSelfUpdateResult(new Models.AlreadyUpToDate());
                        return;
                    }

                    // Should not check ignored tag if this is called manually.
                    if (!manually)
                    {
                        var pref = ViewModels.Preference.Instance;
                        if (ver.TagName == pref.IgnoreUpdateTag)
                            return;
                    }

                    ShowSelfUpdateResult(ver);
                }
                catch (Exception e)
                {
                    if (manually)
                        ShowSelfUpdateResult(e);
                }
            });
        }

        public static void Quit()
        {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow.Close();
                desktop.Shutdown();
            }
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            var pref = ViewModels.Preference.Instance;

            SetLocale(pref.Locale);
            SetTheme(pref.Theme);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                BindingPlugins.DataValidators.RemoveAt(0);

                var launcher = new Views.Launcher();
                _notificationReceiver = launcher;
                desktop.MainWindow = launcher;

                if (ViewModels.Preference.Instance.ShouldCheck4UpdateOnStartup)
                {
                    ViewModels.Preference.Save();
                    Check4Update();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ShowSelfUpdateResult(object data)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var dialog = new Views.SelfUpdate()
                    {
                        DataContext = new ViewModels.SelfUpdate
                        {
                            Data = data
                        }
                    };

                    dialog.Show(desktop.MainWindow);
                }
            });
        }

        private ResourceDictionary _activeLocale = null;
        private Models.INotificationReceiver _notificationReceiver = null;
    }
}
