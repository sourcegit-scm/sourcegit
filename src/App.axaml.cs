using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

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
    public class SimpleCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public SimpleCommand(Action action)
        {
            _action = action;
        }

        public bool CanExecute(object parameter) => _action != null;
        public void Execute(object parameter) => _action?.Invoke();

        private Action _action = null;
    }

    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length > 1 && args[0].Equals("--rebase-editor", StringComparison.Ordinal))
                    Environment.Exit(Models.InteractiveRebaseEditor.Process(args[1]));
                else
                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                var builder = new StringBuilder();
                builder.Append($"Crash::: {ex.GetType().FullName}: {ex.Message}\n\n");
                builder.Append("----------------------------\n");
                builder.Append($"Version: {Assembly.GetExecutingAssembly().GetName().Version}\n");
                builder.Append($"OS: {Environment.OSVersion.ToString()}\n");
                builder.Append($"Framework: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}\n");
                builder.Append($"Source: {ex.Source}\n");
                builder.Append($"---------------------------\n\n");
                builder.Append(ex.StackTrace);
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    builder.Append($"\n\nInnerException::: {ex.GetType().FullName}: {ex.Message}\n");
                    builder.Append(ex.StackTrace);
                }

                var time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var file = Path.Combine(Native.OS.DataDir, $"crash_{time}.log");
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

        public static readonly SimpleCommand OpenPreferenceCommand = new SimpleCommand(() =>
        {
            var dialog = new Views.Preference();
            dialog.ShowDialog(GetTopLevel() as Window);
        });

        public static readonly SimpleCommand OpenHotkeysCommand = new SimpleCommand(() =>
        {
            var dialog = new Views.Hotkeys();
            dialog.ShowDialog(GetTopLevel() as Window);
        });

        public static readonly SimpleCommand OpenAboutCommand = new SimpleCommand(() =>
        {
            var dialog = new Views.About();
            dialog.ShowDialog(GetTopLevel() as Window);
        });

        public static readonly SimpleCommand CheckForUpdateCommand = new SimpleCommand(() =>
        {
            Check4Update(true);
        });

        public static readonly SimpleCommand QuitCommand = new SimpleCommand(Quit);

        public static void RaiseException(string context, string message)
        {
            if (Current is App app && app._launcher != null)
                app._launcher.DispatchNotification(context, message, true);
        }

        public static void SendNotification(string context, string message)
        {
            if (Current is App app && app._launcher != null)
                app._launcher.DispatchNotification(context, message, false);
        }

        public static void SetLocale(string localeKey)
        {
            var app = Current as App;
            var targetLocale = app.Resources[localeKey] as ResourceDictionary;
            if (targetLocale == null || targetLocale == app._activeLocale)
                return;

            if (app._activeLocale != null)
                app.Resources.MergedDictionaries.Remove(app._activeLocale);

            app.Resources.MergedDictionaries.Add(targetLocale);
            app._activeLocale = targetLocale;
        }

        public static void SetTheme(string theme, string colorsFile)
        {
            var app = Current as App;

            if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                app.RequestedThemeVariant = ThemeVariant.Light;
            else if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                app.RequestedThemeVariant = ThemeVariant.Dark;
            else
                app.RequestedThemeVariant = ThemeVariant.Default;

            if (app._colorOverrides != null)
            {
                app.Resources.MergedDictionaries.Remove(app._colorOverrides);
                app._colorOverrides = null;
            }

            if (!string.IsNullOrEmpty(colorsFile) && File.Exists(colorsFile))
            {
                try
                {
                    var resDic = new ResourceDictionary();

                    var schema = JsonSerializer.Deserialize(File.ReadAllText(colorsFile), JsonCodeGen.Default.DictionaryStringString);
                    foreach (var kv in schema)
                        resDic[kv.Key] = Color.Parse(kv.Value);

                    app.Resources.MergedDictionaries.Add(resDic);
                    app._colorOverrides = resDic;
                }
                catch
                {
                }
            }
        }

        public static async void CopyText(string data)
        {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow.Clipboard is { } clipbord)
                    await clipbord.SetTextAsync(data);
            }
        }

        public static async Task<string> GetClipboardTextAsync()
        {
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow.Clipboard is { } clipboard)
                {
                    return await clipboard.GetTextAsync();
                }
            }
            return default;
        }

        public static string Text(string key, params object[] args)
        {
            var fmt = Current.FindResource($"Text.{key}") as string;
            if (string.IsNullOrWhiteSpace(fmt))
                return $"Text.{key}";

            if (args == null || args.Length == 0)
                return fmt;

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
                    var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };
                    var data = await client.GetStringAsync("https://sourcegit-scm.github.io/data/version.json");

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

        public static ViewModels.Repository FindOpenedRepository(string repoPath)
        {
            if (Current is App app && app._launcher != null)
            {
                foreach (var page in app._launcher.Pages)
                {
                    var id = page.Node.Id.Replace("\\", "/");
                    if (id == repoPath && page.Data is ViewModels.Repository repo)
                        return repo;
                }
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
            SetTheme(pref.Theme, pref.ColorOverrides);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                BindingPlugins.DataValidators.RemoveAt(0);
                Native.OS.SetupEnternalTools();

                _launcher = new ViewModels.Launcher();
                desktop.MainWindow = new Views.Launcher() { DataContext = _launcher };

                var pref = ViewModels.Preference.Instance;
                if (pref.ShouldCheck4UpdateOnStartup)
                {
                    pref.Save();
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

        private ViewModels.Launcher _launcher = null;
        private ResourceDictionary _activeLocale = null;
        private ResourceDictionary _colorOverrides = null;
    }
}
