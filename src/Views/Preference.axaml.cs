using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;

namespace SourceGit.Views {
    public partial class Preference : Window {
        public bool CanChangeTitleBarStyle => !OperatingSystem.IsMacOS();

        public string DefaultUser {
            get;
            set;
        }

        public string DefaultEmail {
            get;
            set;
        }

        public Models.CRLFMode CRLFMode {
            get;
            set;
        }

        public bool EnableGPGSigning {
            get;
            set;
        }

        public string GPGExecutableFile {
            get;
            set;
        }

        public string GPGUserKey {
            get;
            set;
        }

        public Preference() {
            var pref = ViewModels.Preference.Instance;
            DataContext = pref;

            var ver = string.Empty;
            if (pref.IsGitConfigured) {
                var config = new Commands.Config(null).ListAll();

                if (config.ContainsKey("user.name")) DefaultUser = config["user.name"];
                if (config.ContainsKey("user.email")) DefaultEmail = config["user.email"];
                if (config.ContainsKey("user.signingkey")) GPGUserKey = config["user.signingkey"];
                if (config.ContainsKey("core.autocrlf")) CRLFMode = Models.CRLFMode.Supported.Find(x => x.Value == config["core.autocrlf"]);
                if (config.ContainsKey("commit.gpgsign")) EnableGPGSigning = (config["commit.gpgsign"] == "true");
                if (config.ContainsKey("gpg.program")) GPGExecutableFile = config["gpg.program"];

                ver = new Commands.Version().Query();
            }

            InitializeComponent();
            txtVersion.Text = ver;
        }

        private void CloseWindow(object sender, RoutedEventArgs e) {
            var cmd = new Commands.Config(null);

            var config = cmd.ListAll();
            var oldUser = config.ContainsKey("user.name") ? config["user.name"] : string.Empty;
            var oldEmail = config.ContainsKey("user.email") ? config["user.email"] : string.Empty;
            var oldGPGSignKey = config.ContainsKey("user.signingkey") ? config["user.signingkey"] : string.Empty;
            var oldCRLF = config.ContainsKey("core.autocrlf") ? config["core.autocrlf"] : string.Empty;
            var oldGPGSignEnable = config.ContainsKey("commit.gpgsign") ? config["commit.gpgsign"] : "false";
            var oldGPGExec = config.ContainsKey("gpg.program") ? config["gpg.program"] : string.Empty;

            if (DefaultUser != oldUser) cmd.Set("user.name", DefaultUser);
            if (DefaultEmail != oldEmail) cmd.Set("user.email", DefaultEmail);
            if (GPGUserKey != oldGPGSignKey) cmd.Set("user.signingkey", GPGUserKey);
            if (CRLFMode.Value != oldCRLF) cmd.Set("core.autocrlf", CRLFMode.Value);
            if (EnableGPGSigning != (oldGPGSignEnable == "true")) cmd.Set("commit.gpgsign", EnableGPGSigning ?  "true" : "false");
            if (GPGExecutableFile != oldGPGExec) cmd.Set("gpg.program", GPGExecutableFile);

            Close();
        }

        private async void SelectGitInstallDir(object sender, RoutedEventArgs e) {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var selected = await StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1) {
                var testExec = Path.Combine(selected[0].Path.LocalPath, "bin", OperatingSystem.IsWindows() ? "git.exe" : "git");
                if (File.Exists(testExec)) {
                    ViewModels.Preference.Instance.GitInstallDir = selected[0].Path.LocalPath;
                    txtVersion.Text = new Commands.Version().Query();
                }
            }
        }

        private async void SelectDefaultCloneDir(object sender, RoutedEventArgs e) {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var selected = await StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1) {
                ViewModels.Preference.Instance.GitDefaultCloneDir = selected[0].Path.LocalPath;
            }
        }

        private async void SelectGPGExecutable(object sender, RoutedEventArgs e) {
            var options = new FilePickerOpenOptions() {
                FileTypeFilter = [new FilePickerFileType("GPG Executable") { Patterns = ["gpg.exe"] }],
                AllowMultiple = false,
            };

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1) {
                GPGExecutableFile = selected[0].Path.LocalPath;
            }
        }

        private async void SelectExternalMergeTool(object sender, RoutedEventArgs e) {
            var type = ViewModels.Preference.Instance.ExternalMergeToolType;
            if (type == 0 || type >= Models.ExternalMergeTools.Supported.Count) {
                ViewModels.Preference.Instance.ExternalMergeToolType = 1;
                type = 1;
            }

            var tool = Models.ExternalMergeTools.Supported[type];
            var options = new FilePickerOpenOptions() {
                FileTypeFilter = [new FilePickerFileType(tool.Name) { Patterns = [tool.Exec] }],
                AllowMultiple = false,
            };

            var selected = await StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1) {
                ViewModels.Preference.Instance.ExternalMergeToolPath = selected[0].Path.LocalPath;
            }
        }
    }
}
