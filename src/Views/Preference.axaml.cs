using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class Preference : ChromelessWindow
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

        public static readonly StyledProperty<string> GitVersionProperty =
            AvaloniaProperty.Register<Preference, string>(nameof(GitVersion));

        public string GitVersion
        {
            get => GetValue(GitVersionProperty);
            set => SetValue(GitVersionProperty, value);
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
            AvaloniaProperty.Register<Preference, Models.GPGFormat>(nameof(GPGFormat), Models.GPGFormat.Supported[0]);

        public Models.GPGFormat GPGFormat
        {
            get => GetValue(GPGFormatProperty);
            set => SetValue(GPGFormatProperty, value);
        }

        public static readonly StyledProperty<string> GPGExecutableFileProperty =
            AvaloniaProperty.Register<Preference, string>(nameof(GPGExecutableFile));

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

        public Preference()
        {
            var pref = ViewModels.Preference.Instance;
            DataContext = pref;

            var ver = string.Empty;
            if (pref.IsGitConfigured())
            {
                var config = new Commands.Config(null).ListAll();

                if (config.TryGetValue("user.name", out var name))
                    DefaultUser = name;
                if (config.TryGetValue("user.email", out var email))
                    DefaultEmail = email;
                if (config.TryGetValue("user.signingkey", out var signingKey))
                    GPGUserKey = signingKey;
                if (config.TryGetValue("core.autocrlf", out var crlf))
                    CRLFMode = Models.CRLFMode.Supported.Find(x => x.Value == crlf);
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

                ver = new Commands.Version().Query();
            }

            InitializeComponent();
            GitVersion = ver;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GPGFormatProperty)
            {
                var config = new Commands.Config(null).ListAll();
                if (GPGFormat.Value == "openpgp" && config.TryGetValue("gpg.program", out var openpgp))
                    GPGExecutableFile = openpgp;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    GPGExecutableFile = gpgProgram;
            }
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            var config = new Commands.Config(null).ListAll();
            SetIfChanged(config, "user.name", DefaultUser);
            SetIfChanged(config, "user.email", DefaultEmail);
            SetIfChanged(config, "user.signingkey", GPGUserKey);
            SetIfChanged(config, "core.autocrlf", CRLFMode != null ? CRLFMode.Value : null);
            SetIfChanged(config, "commit.gpgsign", EnableGPGCommitSigning ? "true" : "false");
            SetIfChanged(config, "tag.gpgsign", EnableGPGTagSigning ? "true" : "false");
            SetIfChanged(config, "gpg.format", GPGFormat.Value);

            if (!GPGFormat.Value.Equals("ssh", StringComparison.Ordinal))
                SetIfChanged(config, $"gpg.{GPGFormat.Value}.program", GPGExecutableFile);

            Close();
        }

        private async void SelectThemeOverrideFile(object _, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Theme Overrides File") { Patterns = ["*.json"] }],
                AllowMultiple = false,
            };

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
            {
                ViewModels.Preference.Instance.ThemeOverrides = selected[0].Path.LocalPath;
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

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
            {
                ViewModels.Preference.Instance.GitInstallPath = selected[0].Path.LocalPath;
                GitVersion = new Commands.Version().Query();
            }

            e.Handled = true;
        }

        private async void SelectDefaultCloneDir(object _1, RoutedEventArgs _2)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    ViewModels.Preference.Instance.GitDefaultCloneDir = selected[0].Path.LocalPath;
                }
            }
            catch (Exception e)
            {
                App.RaiseException(string.Empty, $"Failed to select default clone directory: {e.Message}");
            }
        }

        private async void SelectGPGExecutable(object _1, RoutedEventArgs _2)
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

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
            {
                GPGExecutableFile = selected[0].Path.LocalPath;
            }
        }

        private async void SelectExternalMergeTool(object _1, RoutedEventArgs _2)
        {
            var type = ViewModels.Preference.Instance.ExternalMergeToolType;
            if (type < 0 || type >= Models.ExternalMerger.Supported.Count)
            {
                ViewModels.Preference.Instance.ExternalMergeToolType = 0;
                return;
            }

            var tool = Models.ExternalMerger.Supported[type];
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType(tool.Name) { Patterns = tool.GetPatterns() }],
                AllowMultiple = false,
            };

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
            {
                ViewModels.Preference.Instance.ExternalMergeToolPath = selected[0].Path.LocalPath;
            }
        }

        private void SetIfChanged(Dictionary<string, string> cached, string key, string value)
        {
            bool changed = false;
            if (cached.TryGetValue(key, out var old))
                changed = old != value;
            else if (!string.IsNullOrEmpty(value))
                changed = true;

            if (changed)
                new Commands.Config(null).Set(key, value);
        }

        private void OnUseNativeWindowFrameChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox box)
            {
                ViewModels.Preference.Instance.UseSystemWindowFrame = box.IsChecked == true;
                ViewModels.Preference.Instance.Save();

                var dialog = new ConfirmRestart();
                App.OpenDialog(dialog);
            }

            e.Handled = true;
        }
    }
}
