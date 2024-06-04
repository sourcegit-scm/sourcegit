using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class Preference : Window
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
        }

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
                if (config.TryGetValue("tag.gpgSign", out var gpgTagSign))
                    EnableGPGTagSigning = (gpgTagSign == "true");
                if (config.TryGetValue("gpg.format", out var gpgFormat))
                    GPGFormat = Models.GPGFormat.Supported.Find(x => x.Value == gpgFormat) ?? Models.GPGFormat.Supported[0];

                if (GPGFormat.Value == "opengpg" && config.TryGetValue("gpg.program", out var opengpg))
                    GPGExecutableFile = opengpg;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    GPGExecutableFile = gpgProgram;

                ver = new Commands.Version().Query();
            }

            InitializeComponent();
            GitVersion = ver;
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            var cmd = new Commands.Config(null);

            var config = cmd.ListAll();
            var oldUser = config.TryGetValue("user.name", out var user) ? user : string.Empty;
            var oldEmail = config.TryGetValue("user.email", out var email) ? email : string.Empty;
            var oldGPGSignKey = config.TryGetValue("user.signingkey", out var signingKey) ? signingKey : string.Empty;
            var oldCRLF = config.TryGetValue("core.autocrlf", out var crlf) ? crlf : string.Empty;
            var oldGPGFormat = config.TryGetValue("gpg.format", out var gpgFormat) ? gpgFormat : "opengpg";
            var oldGPGCommitSignEnable = config.TryGetValue("commit.gpgsign", out var gpgCommitSign) ? gpgCommitSign : "false";
            var oldGPGTagSignEnable = config.TryGetValue("tag.gpgSign", out var gpgTagSign) ? gpgTagSign : "false";
            var oldGPGExec = config.TryGetValue("gpg.program", out var program) ? program : string.Empty;

            if (DefaultUser != oldUser)
                cmd.Set("user.name", DefaultUser);
            if (DefaultEmail != oldEmail)
                cmd.Set("user.email", DefaultEmail);
            if (GPGUserKey != oldGPGSignKey)
                cmd.Set("user.signingkey", GPGUserKey);
            if (CRLFMode != null && CRLFMode.Value != oldCRLF)
                cmd.Set("core.autocrlf", CRLFMode.Value);
            if (EnableGPGCommitSigning != (oldGPGCommitSignEnable == "true"))
                cmd.Set("commit.gpgsign", EnableGPGCommitSigning ? "true" : "false");
            if (EnableGPGTagSigning != (oldGPGTagSignEnable == "true"))
                cmd.Set("tag.gpgSign", EnableGPGTagSigning ? "true" : "false");
            if (GPGFormat.Value != oldGPGFormat)
                cmd.Set("gpg.format", GPGFormat.Value);
            if (GPGExecutableFile != oldGPGExec)
                cmd.Set($"gpg.{GPGFormat.Value}.program", GPGExecutableFile);

            Close();
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
    }
}
