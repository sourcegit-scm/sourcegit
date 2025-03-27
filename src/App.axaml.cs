using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;

namespace SourceGit
{
    public partial class App : Application
    {
        #region App Entry Point
        [STAThread]
        public static void Main(string[] args)
        {
            Native.OS.SetupDataDir();

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                LogException(e.ExceptionObject as Exception);
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                e.SetObserved();
            };

            try
            {
                if (TryLaunchAsRebaseTodoEditor(args, out int exitTodo))
                    Environment.Exit(exitTodo);
                else if (TryLaunchAsRebaseMessageEditor(args, out int exitMessage))
                    Environment.Exit(exitMessage);
                else
                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>();
            builder.UsePlatformDetect();
            builder.LogToTrace();
            builder.WithInterFont();
            builder.With(new FontManagerOptions()
            {
                DefaultFamilyName = "fonts:Inter#Inter"
            });
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

        private static void LogException(Exception ex)
        {
            if (ex == null)
                return;

            var builder = new StringBuilder();
            builder.Append($"Crash::: {ex.GetType().FullName}: {ex.Message}\n\n");
            builder.Append("----------------------------\n");
            builder.Append($"Version: {Assembly.GetExecutingAssembly().GetName().Version}\n");
            builder.Append($"OS: {Environment.OSVersion}\n");
            builder.Append($"Framework: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}\n");
            builder.Append($"Source: {ex.Source}\n");
            builder.Append($"Thread Name: {Thread.CurrentThread.Name ?? "Unnamed"}\n");
            builder.Append($"User: {Environment.UserName}\n");
            builder.Append($"App Start Time: {Process.GetCurrentProcess().StartTime}\n");
            builder.Append($"Exception Time: {DateTime.Now}\n");
            builder.Append($"Memory Usage: {Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024} MB\n");
            builder.Append($"---------------------------\n\n");
            builder.Append(ex);

            var time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var file = Path.Combine(Native.OS.DataDir, $"crash_{time}.log");
            File.WriteAllText(file, builder.ToString());
        }
        #endregion

        #region Utility Functions
        public static void OpenDialog(Window window)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
                window.ShowDialog(owner);
        }

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
            if (app == null)
                return;

            var targetLocale = app.Resources[localeKey] as ResourceDictionary;
            if (targetLocale == null || targetLocale == app._activeLocale)
                return;

            if (app._activeLocale != null)
                app.Resources.MergedDictionaries.Remove(app._activeLocale);

            app.Resources.MergedDictionaries.Add(targetLocale);
            app._activeLocale = targetLocale;
        }

        public static void SetTheme(string theme, string themeOverridesFile)
        {
            var app = Current as App;
            if (app == null)
                return;

            if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                app.RequestedThemeVariant = ThemeVariant.Light;
            else if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                app.RequestedThemeVariant = ThemeVariant.Dark;
            else
                app.RequestedThemeVariant = ThemeVariant.Default;

            if (app._themeOverrides != null)
            {
                app.Resources.MergedDictionaries.Remove(app._themeOverrides);
                app._themeOverrides = null;
            }

            if (!string.IsNullOrEmpty(themeOverridesFile) && File.Exists(themeOverridesFile))
            {
                try
                {
                    var resDic = new ResourceDictionary();
                    var overrides = JsonSerializer.Deserialize(File.ReadAllText(themeOverridesFile), JsonCodeGen.Default.ThemeOverrides);
                    foreach (var kv in overrides.BasicColors)
                    {
                        if (kv.Key.Equals("SystemAccentColor", StringComparison.Ordinal))
                            resDic["SystemAccentColor"] = kv.Value;
                        else
                            resDic[$"Color.{kv.Key}"] = kv.Value;
                    }

                    if (overrides.GraphColors.Count > 0)
                        Models.CommitGraph.SetPens(overrides.GraphColors, overrides.GraphPenThickness);
                    else
                        Models.CommitGraph.SetDefaultPens(overrides.GraphPenThickness);

                    Models.Commit.OpacityForNotMerged = overrides.OpacityForNotMergedCommits;

                    app.Resources.MergedDictionaries.Add(resDic);
                    app._themeOverrides = resDic;
                }
                catch
                {
                    // ignore
                }
            }
            else
            {
                Models.CommitGraph.SetDefaultPens();
            }
        }

        public static void SetFonts(string defaultFont, string monospaceFont, bool onlyUseMonospaceFontInEditor)
        {
            var app = Current as App;
            if (app == null)
                return;

            if (app._fontsOverrides != null)
            {
                app.Resources.MergedDictionaries.Remove(app._fontsOverrides);
                app._fontsOverrides = null;
            }

            defaultFont = app.FixFontFamilyName(defaultFont);
            monospaceFont = app.FixFontFamilyName(monospaceFont);

            var resDic = new ResourceDictionary();
            if (!string.IsNullOrEmpty(defaultFont))
                resDic.Add("Fonts.Default", new FontFamily(defaultFont));

            if (string.IsNullOrEmpty(monospaceFont))
            {
                if (!string.IsNullOrEmpty(defaultFont))
                {
                    monospaceFont = $"fonts:SourceGit#JetBrains Mono,{defaultFont}";
                    resDic.Add("Fonts.Monospace", new FontFamily(monospaceFont));
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(defaultFont) && !monospaceFont.Contains(defaultFont, StringComparison.Ordinal))
                    monospaceFont = $"{monospaceFont},{defaultFont}";

                resDic.Add("Fonts.Monospace", new FontFamily(monospaceFont));
            }

            if (onlyUseMonospaceFontInEditor)
            {
                if (string.IsNullOrEmpty(defaultFont))
                    resDic.Add("Fonts.Primary", new FontFamily("fonts:Inter#Inter"));
                else
                    resDic.Add("Fonts.Primary", new FontFamily(defaultFont));
            }
            else
            {
                if (!string.IsNullOrEmpty(monospaceFont))
                    resDic.Add("Fonts.Primary", new FontFamily(monospaceFont));
            }

            if (resDic.Count > 0)
            {
                app.Resources.MergedDictionaries.Add(resDic);
                app._fontsOverrides = resDic;
            }
        }

        public static async void CopyText(string data)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow?.Clipboard is { } clipboard)
                    await clipboard.SetTextAsync(data ?? "");
            }
        }

        public static async Task<string> GetClipboardTextAsync()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow?.Clipboard is { } clipboard)
                {
                    return await clipboard.GetTextAsync();
                }
            }
            return default;
        }

        public static string Text(string key, params object[] args)
        {
            var fmt = Current?.FindResource($"Text.{key}") as string;
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

            var geo = Current?.FindResource(key) as StreamGeometry;
            if (geo != null)
                icon.Data = geo;

            return icon;
        }

        public static IStorageProvider GetStorageProvider()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow?.StorageProvider;

            return null;
        }

        public static ViewModels.Launcher GetLauncer()
        {
            return Current is App app ? app._launcher : null;
        }

        public static void Quit(int exitCode)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Close();
                desktop.Shutdown(exitCode);
            }
            else
            {
                Environment.Exit(exitCode);
            }
        }
        #endregion

        #region Overrides
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            var pref = ViewModels.Preferences.Instance;
            pref.PropertyChanged += (_, _) => pref.Save();

            SetLocale(pref.Locale);
            SetTheme(pref.Theme, pref.ThemeOverrides);
            SetFonts(pref.DefaultFontFamily, pref.MonospaceFontFamily, pref.OnlyUseMonoFontInEditor);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                BindingPlugins.DataValidators.RemoveAt(0);

                if (TryLaunchAsCoreEditor(desktop))
                    return;

                if (TryLaunchAsAskpass(desktop))
                    return;

                TryLaunchAsNormal(desktop);
            }
        }
        #endregion

        private static bool TryLaunchAsRebaseTodoEditor(string[] args, out int exitCode)
        {
            exitCode = -1;

            if (args.Length <= 1 || !args[0].Equals("--rebase-todo-editor", StringComparison.Ordinal))
                return false;

            var file = args[1];
            var filename = Path.GetFileName(file);
            if (!filename.Equals("git-rebase-todo", StringComparison.OrdinalIgnoreCase))
                return true;

            var dirInfo = new DirectoryInfo(Path.GetDirectoryName(file)!);
            if (!dirInfo.Exists || !dirInfo.Name.Equals("rebase-merge", StringComparison.Ordinal))
                return true;

            var jobsFile = Path.Combine(dirInfo.Parent!.FullName, "sourcegit_rebase_jobs.json");
            if (!File.Exists(jobsFile))
                return true;

            var collection = JsonSerializer.Deserialize(File.ReadAllText(jobsFile), JsonCodeGen.Default.InteractiveRebaseJobCollection);
            var lines = new List<string>();
            foreach (var job in collection.Jobs)
            {
                switch (job.Action)
                {
                    case Models.InteractiveRebaseAction.Pick:
                        lines.Add($"p {job.SHA}");
                        break;
                    case Models.InteractiveRebaseAction.Edit:
                        lines.Add($"e {job.SHA}");
                        break;
                    case Models.InteractiveRebaseAction.Reword:
                        lines.Add($"r {job.SHA}");
                        break;
                    case Models.InteractiveRebaseAction.Squash:
                        lines.Add($"s {job.SHA}");
                        break;
                    case Models.InteractiveRebaseAction.Fixup:
                        lines.Add($"f {job.SHA}");
                        break;
                    default:
                        lines.Add($"d {job.SHA}");
                        break;
                }
            }

            File.WriteAllLines(file, lines);

            exitCode = 0;
            return true;
        }

        private static bool TryLaunchAsRebaseMessageEditor(string[] args, out int exitCode)
        {
            exitCode = -1;

            if (args.Length <= 1 || !args[0].Equals("--rebase-message-editor", StringComparison.Ordinal))
                return false;

            exitCode = 0;

            var file = args[1];
            var filename = Path.GetFileName(file);
            if (!filename.Equals("COMMIT_EDITMSG", StringComparison.OrdinalIgnoreCase))
                return true;

            var gitDir = Path.GetDirectoryName(file)!;
            var jobsFile = Path.Combine(gitDir, "sourcegit_rebase_jobs.json");
            if (!File.Exists(jobsFile))
                return true;

            var collection = JsonSerializer.Deserialize(File.ReadAllText(jobsFile), JsonCodeGen.Default.InteractiveRebaseJobCollection);
            var doneFile = Path.Combine(gitDir, "rebase-merge", "done");
            if (!File.Exists(doneFile))
                return true;

            var done = File.ReadAllText(doneFile).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (done.Length > collection.Jobs.Count)
                return true;

            var job = collection.Jobs[done.Length - 1];
            File.WriteAllText(file, job.Message);

            return true;
        }

        private bool TryLaunchAsCoreEditor(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;
            if (args == null || args.Length <= 1 || !args[0].Equals("--core-editor", StringComparison.Ordinal))
                return false;

            var file = args[1];
            if (!File.Exists(file))
            {
                desktop.Shutdown(-1);
                return true;
            }

            var editor = new Views.StandaloneCommitMessageEditor();
            editor.SetFile(file);
            desktop.MainWindow = editor;
            return true;
        }

        private bool TryLaunchAsAskpass(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var launchAsAskpass = Environment.GetEnvironmentVariable("SOURCEGIT_LAUNCH_AS_ASKPASS");
            if (launchAsAskpass is not "TRUE")
                return false;

            var args = desktop.Args;
            if (args?.Length > 0)
            {
                var askpass = new Views.Askpass();
                askpass.TxtDescription.Text = args[0];
                desktop.MainWindow = askpass;
                return true;
            }

            return false;
        }

        private void TryLaunchAsNormal(IClassicDesktopStyleApplicationLifetime desktop)
        {
            Native.OS.SetupEnternalTools();
            Models.AvatarManager.Instance.Start();

            string startupRepo = null;
            if (desktop.Args != null && desktop.Args.Length == 1 && Directory.Exists(desktop.Args[0]))
                startupRepo = desktop.Args[0];

            _launcher = new ViewModels.Launcher(startupRepo);
            desktop.MainWindow = new Views.Launcher() { DataContext = _launcher };

#if !DISABLE_UPDATE_DETECTION
            var pref = ViewModels.Preferences.Instance;
            if (pref.ShouldCheck4UpdateOnStartup())
                Check4Update();
#endif
        }

        private void Check4Update(bool manually = false)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Fetch latest release information.
                    var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };
                    var data = await client.GetStringAsync("https://sourcegit-scm.github.io/data/version.json");

                    // Parse JSON into Models.Version.
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
                        var pref = ViewModels.Preferences.Instance;
                        if (ver.TagName == pref.IgnoreUpdateTag)
                            return;
                    }

                    ShowSelfUpdateResult(ver);
                }
                catch (Exception e)
                {
                    if (manually)
                        ShowSelfUpdateResult(new Models.SelfUpdateFailed(e));
                }
            });
        }

        private void ShowSelfUpdateResult(object data)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
                {
                    var dialog = new Views.SelfUpdate() { DataContext = new ViewModels.SelfUpdate() { Data = data } };
                    dialog.ShowDialog(owner);
                }
            });
        }

        private string FixFontFamilyName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var parts = input.Split(',');
            var trimmed = new List<string>();

            foreach (var part in parts)
            {
                var t = part.Trim();
                if (string.IsNullOrEmpty(t))
                    continue;

                // Collapse multiple spaces into single space
                var prevChar = '\0';
                var sb = new StringBuilder();

                foreach (var c in t)
                {
                    if (c == ' ' && prevChar == ' ')
                        continue;
                    sb.Append(c);
                    prevChar = c;
                }

                trimmed.Add(sb.ToString());
            }

            return trimmed.Count > 0 ? string.Join(',', trimmed) : string.Empty;
        }

        private ViewModels.Launcher _launcher = null;
        private ResourceDictionary _activeLocale = null;
        private ResourceDictionary _themeOverrides = null;
        private ResourceDictionary _fontsOverrides = null;
    }
}
