using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Styling;

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
            var rd = new ResourceDictionary();

            var culture = CultureInfo.GetCultureInfo(localeKey.Replace("_", "-"));
            SourceGit.Resources.Locales.Culture = culture;

            var sets = SourceGit.Resources.Locales.ResourceManager.GetResourceSet(culture, true, true);
            foreach (var obj in sets)
            {
                if (obj is DictionaryEntry entry)
                {
                    rd.Add(entry.Key, entry.Value);
                }
            }

            if (app._activeLocale != null)
            {
                app.Resources.MergedDictionaries.Remove(app._activeLocale);
            }

            app.Resources.MergedDictionaries.Add(rd);
            app._activeLocale = rd;
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
            if (string.IsNullOrWhiteSpace(fmt)) return $"Text.{key}";
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

            if (string.IsNullOrEmpty(pref.DefaultFont))
            {
                pref.DefaultFont = FontManager.Current.DefaultFontFamily.ToString();
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
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