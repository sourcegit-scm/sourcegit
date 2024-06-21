using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class Preference : ChromelessWindow
    {
        public AvaloniaList<FontFamily> InstalledFonts
        {
            get;
            private set;
        }

        public AvaloniaList<FontFamily> InstalledMonospaceFonts
        {
            get;
            private set;
        }

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
        } = Models.CRLFMode.Supported[0];

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

            var builtInMono = new FontFamily("fonts:SourceGit#JetBrains Mono");

            InstalledFonts = new AvaloniaList<FontFamily>();
            InstalledFonts.Add(builtInMono);
            InstalledFonts.AddRange(FontManager.Current.SystemFonts);

            InstalledMonospaceFonts = new AvaloniaList<FontFamily>();
            InstalledMonospaceFonts.Add(builtInMono);

            var curMonoFont = pref.MonospaceFont;
            if (curMonoFont != builtInMono)
            {
                InstalledMonospaceFonts.Add(curMonoFont);
            }

            Task.Run(() =>
            {
                var sysMonoFonts = new List<FontFamily>();
                foreach (var font in FontManager.Current.SystemFonts)
                {
                    if (font == curMonoFont)
                        continue;

                    var typeface = new Typeface(font);
                    var testI = new FormattedText(
                                "i",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                12,
                                Brushes.White);
                    var testW = new FormattedText(
                                "W",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                12,
                                Brushes.White);
                    if (testI.Width == testW.Width)
                    {
                        sysMonoFonts.Add(font);
                    }
                }

                Dispatcher.UIThread.Post(() => InstalledMonospaceFonts.AddRange(sysMonoFonts));
            });

            var ver = string.Empty;
            if (pref.IsGitConfigured)
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

                foreach (var supportedSingleFormat in Models.GPGFormat.Supported)
                {
                    _gpgExecutableFiles[supportedSingleFormat.Value] = config.GetValueOrDefault($"gpg.{supportedSingleFormat.Value}.program");
                }
                if (config.TryGetValue("gpg.program", out var defaultGpgProgram))
                    _gpgExecutableFiles[Models.GPGFormat.Supported[0].Value] = defaultGpgProgram;
                GPGExecutableFile = _gpgExecutableFiles[GPGFormat.Value];

                ver = new Commands.Version().Query();
            }

            this.PropertyChanged += PreferencePropertyChanged;
            InitializeComponent();
            GitVersion = ver;
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            var config = new Commands.Config(null).ListAll();
            SetIfChanged(config, "user.name", DefaultUser);
            SetIfChanged(config, "user.email", DefaultEmail);
            SetIfChanged(config, "user.signingkey", GPGUserKey);
            SetIfChanged(config, "core.autocrlf", CRLFMode.Value);
            SetIfChanged(config, "commit.gpgsign", EnableGPGCommitSigning ? "true" : "false");
            SetIfChanged(config, "tag.gpgsign", EnableGPGTagSigning ? "true" : "false");
            SetIfChanged(config, "gpg.format", GPGFormat.Value);
            SetIfChanged(config, $"gpg.{GPGFormat.Value}.program", GPGExecutableFile);

            Close();
        }

        private async void SelectColorSchemaFile(object sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Theme Color Schema File") { Patterns = ["*.json"] }],
                AllowMultiple = false,
            };

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
            {
                ViewModels.Preference.Instance.ColorOverrides = selected[0].Path.LocalPath;
            }

            e.Handled = true;
        }

        private async void SelectGitExecutable(object sender, RoutedEventArgs e)
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

        private async void SelectDefaultCloneDir(object sender, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var selected = await StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
            {
                ViewModels.Preference.Instance.GitDefaultCloneDir = selected[0].Path.LocalPath;
            }
        }

        private async void SelectGPGExecutable(object sender, RoutedEventArgs e)
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

        private async void SelectExternalMergeTool(object sender, RoutedEventArgs e)
        {
            var type = ViewModels.Preference.Instance.ExternalMergeToolType;
            if (type < 0 || type >= Models.ExternalMerger.Supported.Count)
            {
                ViewModels.Preference.Instance.ExternalMergeToolType = 0;
                type = 0;
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

        private void PreferencePropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == GPGExecutableFileProperty)
                _gpgExecutableFiles[GPGFormat.Value] = (string)e.NewValue;
            else if (e.Property == GPGFormatProperty)
            {
                var newValue = _gpgExecutableFiles[GPGFormat.Value];
                if (GPGExecutableFile != newValue)
                    GPGExecutableFile = newValue;
            }
        }

        private readonly Dictionary<string, string> _gpgExecutableFiles = new();
    }
}
