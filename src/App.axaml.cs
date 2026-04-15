using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
        #region App Entry Point
        [STAThread]
        public static void Main(string[] args)
        {
            Native.OS.SetupDataDir();

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Native.OS.LogException(e.ExceptionObject as Exception);
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
                Native.OS.LogException(ex);
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
        #endregion

        #region Utility Functions
        public static async Task<bool> AskConfirmAsync(string message, Models.ConfirmButtonType buttonType = Models.ConfirmButtonType.OkCancel)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            {
                var confirm = new Views.Confirm();
                confirm.SetData(message, buttonType);
                return await confirm.ShowDialog<bool>(owner);
            }

            return false;
        }

        public static async Task<Models.ConfirmEmptyCommitResult> AskConfirmEmptyCommitAsync(bool hasLocalChanges, bool hasSelectedUnstaged)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            {
                var confirm = new Views.ConfirmEmptyCommit();
                confirm.TxtMessage.Text = Text(hasLocalChanges ? "ConfirmEmptyCommit.WithLocalChanges" : "ConfirmEmptyCommit.NoLocalChanges");
                confirm.BtnStageAllAndCommit.IsVisible = hasLocalChanges;
                confirm.BtnStageSelectedAndCommit.IsVisible = hasSelectedUnstaged;
                return await confirm.ShowDialog<Models.ConfirmEmptyCommitResult>(owner);
            }

            return Models.ConfirmEmptyCommitResult.Cancel;
        }

        public static void SetLocale(string localeKey)
        {
            if (Current is not App app ||
                app.Resources[localeKey] is not ResourceDictionary targetLocale ||
                targetLocale == app._activeLocale)
                return;

            if (app._activeLocale != null)
                app.Resources.MergedDictionaries.Remove(app._activeLocale);

            app.Resources.MergedDictionaries.Add(targetLocale);
            app._activeLocale = targetLocale;
        }

        public static void SetTheme(string theme, string themeOverridesFile)
        {
            if (Current is not App app)
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
                    using var stream = File.OpenRead(themeOverridesFile);
                    var overrides = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.ThemeOverrides);
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

                    Native.OS.UseMicaOnWindows11 = overrides.UseMicaOnWindows11;

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

        public static void SetFonts(string defaultFont, string monospaceFont)
        {
            if (Current is not App app)
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
                    resDic.Add("Fonts.Monospace", FontFamily.Parse(monospaceFont));
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(defaultFont) && !monospaceFont.Contains(defaultFont, StringComparison.Ordinal))
                    monospaceFont = $"{monospaceFont},{defaultFont}";

                resDic.Add("Fonts.Monospace", FontFamily.Parse(monospaceFont));
            }

            if (resDic.Count > 0)
            {
                app.Resources.MergedDictionaries.Add(resDic);
                app._fontsOverrides = resDic;
            }
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

        public static ViewModels.Launcher GetLauncher()
        {
            return Current is App app ? app._launcher : null;
        }

        public static void Quit(int exitCode)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
            SetFonts(pref.DefaultFontFamily, pref.MonospaceFontFamily);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                BindingPlugins.DataValidators.RemoveAt(0);

                // Disable tooltip if window is not active.
                ToolTip.ToolTipOpeningEvent.AddClassHandler<Control>((c, e) =>
                {
                    var topLevel = TopLevel.GetTopLevel(c);
                    if (topLevel is not Window { IsActive: true })
                        e.Cancel = true;
                });

                if (TryLaunchAsFileHistoryViewer(desktop))
                    return;

                if (TryLaunchAsBlameViewer(desktop))
                    return;

                if (TryLaunchAsCoreEditor(desktop))
                    return;

                if (TryLaunchAsAskpass(desktop))
                    return;

                TryLaunchAsNormal(desktop);
            }
        }
        #endregion

        #region Launch Ways
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

            var jobsFile = Path.Combine(dirInfo.Parent!.FullName, "sourcegit.interactive_rebase");
            if (!File.Exists(jobsFile))
                return true;

            using var stream = File.OpenRead(jobsFile);
            var collection = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            collection.WriteTodoList(file);
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
            var origHeadFile = Path.Combine(gitDir, "rebase-merge", "orig-head");
            var ontoFile = Path.Combine(gitDir, "rebase-merge", "onto");
            var doneFile = Path.Combine(gitDir, "rebase-merge", "done");
            var jobsFile = Path.Combine(gitDir, "sourcegit.interactive_rebase");
            if (!File.Exists(ontoFile) || !File.Exists(origHeadFile) || !File.Exists(doneFile) || !File.Exists(jobsFile))
                return true;

            var origHead = File.ReadAllText(origHeadFile).Trim();
            var onto = File.ReadAllText(ontoFile).Trim();
            using var stream = File.OpenRead(jobsFile);
            var collection = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            if (collection.Onto.StartsWith(onto, StringComparison.OrdinalIgnoreCase) && collection.OrigHead.StartsWith(origHead, StringComparison.OrdinalIgnoreCase))
                collection.WriteCommitMessage(doneFile, file);

            return true;
        }

        private bool TryLaunchAsFileHistoryViewer(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;
            if (args is not { Length: > 1 } || !args[0].Equals("--file-history", StringComparison.Ordinal))
                return false;

            var file = Path.GetFullPath(args[1]);
            var dir = Path.GetDirectoryName(file);

            var test = new Commands.QueryRepositoryRootPath(dir).GetResult();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                Console.Out.WriteLine($"'{args[1]}' is not in a valid git repository");
                desktop.Shutdown(-1);
                return true;
            }

            var repo = test.StdOut.Trim();
            var relFile = Path.GetRelativePath(repo, file);
            var viewer = new Views.FileHistories()
            {
                DataContext = new ViewModels.FileHistories(repo, relFile)
            };
            desktop.MainWindow = viewer;
            return true;
        }

        private bool TryLaunchAsBlameViewer(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;
            if (args is not { Length: > 1 } || !args[0].Equals("--blame", StringComparison.Ordinal))
                return false;

            var file = Path.GetFullPath(args[1]);
            var dir = Path.GetDirectoryName(file);

            var test = new Commands.QueryRepositoryRootPath(dir).GetResult();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                Console.Out.WriteLine($"'{args[1]}' is not in a valid git repository");
                desktop.Shutdown(-1);
                return true;
            }

            var repo = test.StdOut.Trim();
            var head = new Commands.QuerySingleCommit(repo, "HEAD").GetResult();
            if (head == null)
            {
                Console.Out.WriteLine($"{repo} has no commits!");
                desktop.Shutdown(-1);
                return true;
            }

            var relFile = Path.GetRelativePath(repo, file);
            var viewer = new Views.Blame()
            {
                DataContext = new ViewModels.Blame(repo, relFile, head)
            };
            desktop.MainWindow = viewer;
            return true;
        }

        private bool TryLaunchAsCoreEditor(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;
            if (args is not { Length: > 1 } || !args[0].Equals("--core-editor", StringComparison.Ordinal))
                return false;

            var file = args[1];
            if (!File.Exists(file))
            {
                desktop.Shutdown(-1);
                return true;
            }

            var editor = new Views.CommitMessageEditor();
            editor.AsStandalone(file);
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
            _ipcChannel = new Models.IpcChannel();
            if (!_ipcChannel.IsFirstInstance)
            {
                var arg = desktop.Args is { Length: > 0 } ? desktop.Args[0].Trim() : string.Empty;
                if (!string.IsNullOrEmpty(arg))
                {
                    if (arg.StartsWith('"') && arg.EndsWith('"'))
                        arg = arg.Substring(1, arg.Length - 2).Trim();

                    if (arg.Length > 0 && !Path.IsPathFullyQualified(arg))
                        arg = Path.GetFullPath(arg);
                }

                _ipcChannel.SendToFirstInstance(arg);
                Environment.Exit(0);
                return;
            }

            Native.OS.SetupExternalTools();
            Models.AvatarManager.Instance.Start();

            string startupRepo = null;
            if (desktop.Args is { Length: 1 } && Directory.Exists(desktop.Args[0]))
                startupRepo = desktop.Args[0];

            var pref = ViewModels.Preferences.Instance;
            pref.SetCanModify();
            pref.UpdateAvailableAIModels();

            _launcher = new ViewModels.Launcher(startupRepo);
            desktop.MainWindow = new Views.Launcher() { DataContext = _launcher };
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            _ipcChannel.MessageReceived += repo =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _launcher.TryOpenRepositoryFromPath(repo);
                    if (desktop.MainWindow is Views.Launcher main)
                        main.BringToTop();
                });
            };

            desktop.Exit += (_, _) => _ipcChannel.Dispose();

#if !DISABLE_UPDATE_DETECTION
            if (pref.ShouldCheck4UpdateOnStartup())
                Check4Update();
#endif
        }
        #endregion

        #region Check for Updates
        private void Check4Update(bool manually = false)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Fetch latest release information.
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var data = await client.GetStringAsync("https://sourcegit-scm.github.io/data/version.json");
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
            try
            {
                Dispatcher.UIThread.Invoke(async () =>
                {
                    if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
                    {
                        var ctx = new ViewModels.SelfUpdate { Data = data };
                        var dialog = new Views.SelfUpdate() { DataContext = ctx };
                        await dialog.ShowDialog(owner);
                    }
                });
            }
            catch
            {
                // Ignore exceptions.
            }
        }
        #endregion

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

                var sb = new StringBuilder();
                var prevChar = '\0';

                foreach (var c in t)
                {
                    if (c == ' ' && prevChar == ' ')
                        continue;
                    sb.Append(c);
                    prevChar = c;
                }

                var name = sb.ToString();
                try
                {
                    var fontFamily = FontFamily.Parse(name);
                    if (fontFamily.FamilyTypefaces.Count > 0)
                        trimmed.Add(name);
                }
                catch
                {
                    // Ignore exceptions.
                }
            }

            return trimmed.Count > 0 ? string.Join(',', trimmed) : string.Empty;
        }

        private Models.IpcChannel _ipcChannel = null;
        private ViewModels.Launcher _launcher = null;
        private ResourceDictionary _activeLocale = null;
        private ResourceDictionary _themeOverrides = null;
        private ResourceDictionary _fontsOverrides = null;
    }
}
