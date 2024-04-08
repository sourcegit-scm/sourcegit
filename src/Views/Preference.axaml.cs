using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using SourceGit.Native;

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

        public AvaloniaList<string> ExternalTerminals
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

        public bool EnableGPGSigning
        {
            get;
            set;
        }

        public string GPGExecutableFile
        {
            get;
            set;
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

            ExternalTerminals = ["", ..OS.ExternalTerminals.Select(x => x.Name)];

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
                if (config.TryGetValue("commit.gpgsign", out var gpgsign))
                    EnableGPGSigning = (gpgsign == "true");
                if (config.TryGetValue("gpg.program", out var gpgProgram))
                    GPGExecutableFile = gpgProgram;

                ver = new Commands.Version().Query();
            }

            InitializeComponent();
            txtVersion.Text = ver;
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
            var oldGPGSignEnable = config.TryGetValue("commit.gpgsign", out var gpgsign) ? gpgsign : "false";
            var oldGPGExec = config.TryGetValue("gpg.program", out var program) ? program : string.Empty;

            if (DefaultUser != oldUser)
                cmd.Set("user.name", DefaultUser);
            if (DefaultEmail != oldEmail)
                cmd.Set("user.email", DefaultEmail);
            if (GPGUserKey != oldGPGSignKey)
                cmd.Set("user.signingkey", GPGUserKey);
            if (CRLFMode != null && CRLFMode.Value != oldCRLF)
                cmd.Set("core.autocrlf", CRLFMode.Value);
            if (EnableGPGSigning != (oldGPGSignEnable == "true"))
                cmd.Set("commit.gpgsign", EnableGPGSigning ? "true" : "false");
            if (GPGExecutableFile != oldGPGExec)
                cmd.Set("gpg.program", GPGExecutableFile);

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
                txtVersion.Text = new Commands.Version().Query();
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
            var pattern = OperatingSystem.IsWindows() ? "gpg.exe" : "gpg";
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("GPG Executable") { Patterns = [pattern] }],
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
            if (type < 0 || type >= Models.ExternalMergeTools.Supported.Count)
            {
                ViewModels.Preference.Instance.ExternalMergeToolType = 0;
                type = 0;
            }

            var tool = Models.ExternalMergeTools.Supported[type];
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
    
    public sealed class TerminalOrShellNameToBitmapConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var iconKey = value is string terminalName && !string.IsNullOrWhiteSpace(terminalName) ?
                terminalName :
                GetSystemDefaultTerminalIconKey();

            var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/ExternalTerminalIcons/{iconKey}.png", UriKind.RelativeOrAbsolute));
            return  new Bitmap(icon);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private string GetSystemDefaultTerminalIconKey()
        {
            if (OperatingSystem.IsWindows())
            {
                return "git-bash";
            }
            else if (OperatingSystem.IsMacOS())
            {
                return "osascript";
            }
            else if (OperatingSystem.IsLinux())
            {
                return "gnome-terminal";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
    
    public sealed class TerminalOrShellNameToLocalizedNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string terminalName && !string.IsNullOrWhiteSpace(terminalName) ?
                App.Text($"Preference.General.DefaultTerminalOrShell.{terminalName}") :
                App.Text($"Preference.General.DefaultTerminalOrShell");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
