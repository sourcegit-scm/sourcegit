using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SourceGit {

    /// <summary>
    ///     Application.
    /// </summary>
    public partial class App : Application {
        /// <summary>
        ///     Getter/Setter for application user setting.
        /// </summary>
        public static Preference Setting { get; set; }

        /// <summary>
        ///     Check if GIT has been configured.
        /// </summary>
        public static bool IsGitConfigured {
            get {
                return !string.IsNullOrEmpty(Setting.Tools.GitExecutable)
                    && File.Exists(Setting.Tools.GitExecutable);
            }
        }

        /// <summary>
        ///     Raise error message.
        /// </summary>
        /// <param name="message"></param>
        public static void RaiseError(string msg) {
            Current.Dispatcher.Invoke(() => {
                (Current.MainWindow as UI.Launcher).Errors.Add(msg);
            });
        }

        /// <summary>
        ///     Open repository.
        /// </summary>
        /// <param name="repo"></param>
        public static void Open(Git.Repository repo) {
            (Current.MainWindow as UI.Launcher).Open(repo);
        }

        /// <summary>
        ///     Save settings.
        /// </summary>
        public static void SaveSetting() {
            var settingFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SourceGit",
                "preference.json");

            var dir = Path.GetDirectoryName(settingFile);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var data = JsonSerializer.Serialize(Setting, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(settingFile, data);
        }

        /// <summary>
        ///     Startup event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAppStartup(object sender, StartupEventArgs e) {
            // Use this app as a sequence editor?
            if (OpenAsEditor(e)) return;

            // Load settings.
            var settingFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SourceGit",
                "preference.json");
            if (!File.Exists(settingFile)) {
                Setting = new Preference();
            } else {
                Setting = JsonSerializer.Deserialize<Preference>(File.ReadAllText(settingFile));
            }

            // Try auto configure git via registry.
            if (!IsGitConfigured) {
                var root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

                var git = root.OpenSubKey("SOFTWARE\\GitForWindows");
                if (git != null) {
                    Setting.Tools.GitExecutable = Path.Combine(
                        git.GetValue("InstallPath") as string,
                        "bin",
                        "git.exe");
                }
            }

            // Apply themes
            if (Setting.UI.UseLightTheme) {
                foreach (var rs in Current.Resources.MergedDictionaries) {
                    if (rs.Source != null && rs.Source.OriginalString.StartsWith("pack://application:,,,/Resources/Themes/")) {
                        rs.Source = new Uri("pack://application:,,,/Resources/Themes/Light.xaml", UriKind.Absolute);
                        break;
                    }
                }
            }

            // Show main window
            Current.MainWindow = new UI.Launcher();
            Current.MainWindow.Show();
        }

        /// <summary>
        ///     Deactivated event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAppDeactivated(object sender, EventArgs e) {
            GC.Collect();
            SaveSetting();            
        }

        /// <summary>
        ///     Try to open app as git editor
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool OpenAsEditor(StartupEventArgs e) {
            if (e.Args.Length < 3) return false;

            switch (e.Args[0]) {
            case "--sequence":
                var output = File.CreateText(e.Args[2]);
                output.Write(File.ReadAllText(e.Args[1]));
                output.Flush();
                output.Close();

                Environment.Exit(0);
                break;
            default:
                return false;
            }

            return true;
        }
    }
}
