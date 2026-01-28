using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class Preferences : ChromelessWindow
    {
        public string DefaultUser
        {
            get;
            set;
        }

        public string DefaultEmail
        {
            get;
            set;
        }

        public Models.CRLFMode CRLFMode
        {
            get;
            set;
        } = null;

        public bool EnablePruneOnFetch
        {
            get;
            set;
        }

        public static readonly StyledProperty<string> GitVersionProperty =
            AvaloniaProperty.Register<Preferences, string>(nameof(GitVersion));

        public string GitVersion
        {
            get => GetValue(GitVersionProperty);
            set => SetValue(GitVersionProperty, value);
        }

        public static readonly StyledProperty<bool> ShowGitVersionWarningProperty =
            AvaloniaProperty.Register<Preferences, bool>(nameof(ShowGitVersionWarning));

        public bool ShowGitVersionWarning
        {
            get => GetValue(ShowGitVersionWarningProperty);
            set => SetValue(ShowGitVersionWarningProperty, value);
        }

        public bool EnableGPGCommitSigning
        {
            get;
            set;
        }

        public bool EnableGPGTagSigning
        {
            get;
            set;
        }

        public static readonly StyledProperty<Models.GPGFormat> GPGFormatProperty =
            AvaloniaProperty.Register<Preferences, Models.GPGFormat>(nameof(GPGFormat), Models.GPGFormat.Supported[0]);

        public Models.GPGFormat GPGFormat
        {
            get => GetValue(GPGFormatProperty);
            set => SetValue(GPGFormatProperty, value);
        }

        public static readonly StyledProperty<string> GPGExecutableFileProperty =
            AvaloniaProperty.Register<Preferences, string>(nameof(GPGExecutableFile));

        public string GPGExecutableFile
        {
            get => GetValue(GPGExecutableFileProperty);
            set => SetValue(GPGExecutableFileProperty, value);
        }

        public string GPGUserKey
        {
            get;
            set;
        }

        public bool EnableHTTPSSLVerify
        {
            get;
            set;
        } = false;

        public static readonly StyledProperty<Models.OpenAIService> SelectedOpenAIServiceProperty =
            AvaloniaProperty.Register<Preferences, Models.OpenAIService>(nameof(SelectedOpenAIService));

        public Models.OpenAIService SelectedOpenAIService
        {
            get => GetValue(SelectedOpenAIServiceProperty);
            set => SetValue(SelectedOpenAIServiceProperty, value);
        }

        public static readonly StyledProperty<Models.CustomAction> SelectedCustomActionProperty =
            AvaloniaProperty.Register<Preferences, Models.CustomAction>(nameof(SelectedCustomAction));

        public Models.CustomAction SelectedCustomAction
        {
            get => GetValue(SelectedCustomActionProperty);
            set => SetValue(SelectedCustomActionProperty, value);
        }

        public Preferences()
        {
            var pref = ViewModels.Preferences.Instance;
            DataContext = pref;
            CloseOnESC = true;

            if (pref.IsGitConfigured())
            {
                var config = new Commands.Config(null).ReadAll();

                if (config.TryGetValue("user.name", out var name))
                    DefaultUser = name;
                if (config.TryGetValue("user.email", out var email))
                    DefaultEmail = email;
                if (config.TryGetValue("user.signingkey", out var signingKey))
                    GPGUserKey = signingKey;
                if (config.TryGetValue("core.autocrlf", out var crlf))
                    CRLFMode = Models.CRLFMode.Supported.Find(x => x.Value == crlf);
                if (config.TryGetValue("fetch.prune", out var pruneOnFetch))
                    EnablePruneOnFetch = (pruneOnFetch == "true");
                if (config.TryGetValue("commit.gpgsign", out var gpgCommitSign))
                    EnableGPGCommitSigning = (gpgCommitSign == "true");
                if (config.TryGetValue("tag.gpgsign", out var gpgTagSign))
                    EnableGPGTagSigning = (gpgTagSign == "true");
                if (config.TryGetValue("gpg.format", out var gpgFormat))
                    GPGFormat = Models.GPGFormat.Supported.Find(x => x.Value == gpgFormat) ?? Models.GPGFormat.Supported[0];

                if (GPGFormat.Value == "openpgp" && config.TryGetValue("gpg.program", out var openpgp))
                    GPGExecutableFile = openpgp;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    GPGExecutableFile = gpgProgram;

                if (config.TryGetValue("http.sslverify", out var sslVerify))
                    EnableHTTPSSLVerify = sslVerify == "true";
                else
                    EnableHTTPSSLVerify = true;
            }

            UpdateGitVersion();
            InitializeComponent();
        }

        protected override async void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GPGFormatProperty)
            {
                var config = await new Commands.Config(null).ReadAllAsync();
                if (GPGFormat.Value == "openpgp" && config.TryGetValue("gpg.program", out var openpgp))
                    GPGExecutableFile = openpgp;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    GPGExecutableFile = gpgProgram;
            }
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (Design.IsDesignMode)
                return;

            var config = await new Commands.Config(null).ReadAllAsync();
            await SetIfChangedAsync(config, "user.name", DefaultUser, "");
            await SetIfChangedAsync(config, "user.email", DefaultEmail, "");
            await SetIfChangedAsync(config, "user.signingkey", GPGUserKey, "");
            await SetIfChangedAsync(config, "core.autocrlf", CRLFMode?.Value, null);
            await SetIfChangedAsync(config, "fetch.prune", EnablePruneOnFetch ? "true" : "false", "false");
            await SetIfChangedAsync(config, "commit.gpgsign", EnableGPGCommitSigning ? "true" : "false", "false");
            await SetIfChangedAsync(config, "tag.gpgsign", EnableGPGTagSigning ? "true" : "false", "false");
            await SetIfChangedAsync(config, "http.sslverify", EnableHTTPSSLVerify ? "" : "false", "");
            await SetIfChangedAsync(config, "gpg.format", GPGFormat.Value, "openpgp");

            if (!GPGFormat.Value.Equals("ssh", StringComparison.Ordinal))
            {
                var oldGPG = string.Empty;
                if (GPGFormat.Value == "openpgp" && config.TryGetValue("gpg.program", out var openpgp))
                    oldGPG = openpgp;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    oldGPG = gpgProgram;

                bool changed = false;
                if (!string.IsNullOrEmpty(oldGPG))
                    changed = oldGPG != GPGExecutableFile;
                else if (!string.IsNullOrEmpty(GPGExecutableFile))
                    changed = true;

                if (changed)
                    await new Commands.Config(null).SetAsync($"gpg.{GPGFormat.Value}.program", GPGExecutableFile);
            }

            ViewModels.Preferences.Instance.Save();
        }

        private async void SelectThemeOverrideFile(object _, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Theme Overrides File") { Patterns = ["*.json"] }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    ViewModels.Preferences.Instance.ThemeOverrides = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select theme: {ex.Message}");
            }

            e.Handled = true;
        }

        private async void SelectGitExecutable(object _, RoutedEventArgs e)
        {
            var pattern = OperatingSystem.IsWindows() ? "git.exe" : "git";
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Git Executable") { Patterns = [pattern] }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                {
                    ViewModels.Preferences.Instance.GitInstallPath = selected[0].Path.LocalPath;
                    UpdateGitVersion();
                }
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select git executable: {ex.Message}");
            }

            e.Handled = true;
        }

        private async void SelectDefaultCloneDir(object _, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    ViewModels.Preferences.Instance.GitDefaultCloneDir = folderPath;
                }
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select default clone directory: {ex.Message}");
            }

            e.Handled = true;
        }

        private async void SelectGPGExecutable(object _, RoutedEventArgs e)
        {
            var patterns = new List<string>();
            if (OperatingSystem.IsWindows())
                patterns.Add($"{GPGFormat.Program}.exe");
            else
                patterns.Add(GPGFormat.Program);

            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("GPG Program") { Patterns = patterns }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    GPGExecutableFile = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select gpg program: {ex.Message}");
            }

            e.Handled = true;
        }

        private async void SelectShellOrTerminal(object _, RoutedEventArgs e)
        {
            var type = ViewModels.Preferences.Instance.ShellOrTerminalType;
            if (type == -1)
                return;

            var shell = Models.ShellOrTerminal.Supported[type];
            var options = new FilePickerOpenOptions() { AllowMultiple = false };
            if (shell.Type != "custom")
            {
                options = new FilePickerOpenOptions()
                {
                    FileTypeFilter = [new FilePickerFileType(shell.Name) { Patterns = [shell.Exec] }],
                    AllowMultiple = false,
                };
            }

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    ViewModels.Preferences.Instance.ShellOrTerminalPath = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select shell/terminal: {ex.Message}");
            }

            e.Handled = true;
        }

        private async void SelectExternalMergeTool(object _, RoutedEventArgs e)
        {
            var type = ViewModels.Preferences.Instance.ExternalMergeToolType;
            if (type <= 0 || type >= Models.ExternalMerger.Supported.Count)
            {
                ViewModels.Preferences.Instance.ExternalMergeToolType = 0;
                e.Handled = true;
                return;
            }

            var tool = Models.ExternalMerger.Supported[type];
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType(tool.Name) { Patterns = tool.GetPatternsToFindExecFile() }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    ViewModels.Preferences.Instance.ExternalMergeToolPath = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select merge tool: {ex.Message}");
            }

            e.Handled = true;
        }

        private static async Task SetIfChangedAsync(Dictionary<string, string> cached, string key, string value, string defValue)
        {
            bool changed = false;
            if (cached.TryGetValue(key, out var old))
                changed = old != value;
            else if (!string.IsNullOrEmpty(value) && value != defValue)
                changed = true;

            if (changed)
                await new Commands.Config(null).SetAsync(key, value);
        }

        private async void OnUseNativeWindowFrameChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox box)
            {
                ViewModels.Preferences.Instance.UseSystemWindowFrame = box.IsChecked == true;
                await App.ShowDialog(new ConfirmRestart());
            }

            e.Handled = true;
        }

        private void OnGitInstallPathChanged(object sender, TextChangedEventArgs e)
        {
            UpdateGitVersion();
        }

        private void OnAddOpenAIService(object sender, RoutedEventArgs e)
        {
            var service = new Models.OpenAIService() { Name = "Unnamed Service" };
            ViewModels.Preferences.Instance.OpenAIServices.Add(service);
            SelectedOpenAIService = service;

            e.Handled = true;
        }

        private void OnRemoveSelectedOpenAIService(object sender, RoutedEventArgs e)
        {
            if (SelectedOpenAIService == null)
                return;

            ViewModels.Preferences.Instance.OpenAIServices.Remove(SelectedOpenAIService);
            SelectedOpenAIService = null;
            e.Handled = true;
        }

        private void OnAddCustomAction(object sender, RoutedEventArgs e)
        {
            var action = new Models.CustomAction() { Name = "Unnamed Action (Global)" };
            ViewModels.Preferences.Instance.CustomActions.Add(action);
            SelectedCustomAction = action;

            e.Handled = true;
        }

        private async void SelectExecutableForCustomAction(object sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Executable file(script)") { Patterns = ["*.*"] }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 } && sender is Button { DataContext: Models.CustomAction action })
                    action.Executable = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, $"Failed to select program for custom action: {ex.Message}");
            }

            e.Handled = true;
        }

        private void OnRemoveSelectedCustomAction(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomAction == null)
                return;

            ViewModels.Preferences.Instance.CustomActions.Remove(SelectedCustomAction);
            SelectedCustomAction = null;
            e.Handled = true;
        }

        private void OnMoveSelectedCustomActionUp(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomAction == null)
                return;

            var idx = ViewModels.Preferences.Instance.CustomActions.IndexOf(SelectedCustomAction);
            if (idx > 0)
                ViewModels.Preferences.Instance.CustomActions.Move(idx - 1, idx);

            e.Handled = true;
        }

        private void OnMoveSelectedCustomActionDown(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomAction == null)
                return;

            var idx = ViewModels.Preferences.Instance.CustomActions.IndexOf(SelectedCustomAction);
            if (idx < ViewModels.Preferences.Instance.CustomActions.Count - 1)
                ViewModels.Preferences.Instance.CustomActions.Move(idx + 1, idx);

            e.Handled = true;
        }

        private async void EditCustomActionControls(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: Models.CustomAction act })
                return;

            var dialog = new ConfigureCustomActionControls()
            {
                DataContext = new ViewModels.ConfigureCustomActionControls(act.Controls)
            };

            await dialog.ShowDialog(this);
            e.Handled = true;
        }

        private void UpdateGitVersion()
        {
            GitVersion = Native.OS.GitVersionString;
            ShowGitVersionWarning = !string.IsNullOrEmpty(GitVersion) && Native.OS.GitVersion < Models.GitVersions.MINIMAL;
        }
    }
}
