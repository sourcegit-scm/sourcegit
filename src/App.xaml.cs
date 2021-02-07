using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        ///     Load text from locales.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string Text(string key) {
            return Current.FindResource("Text." + key) as string;
        }

        /// <summary>
        ///     Format text
        /// </summary>
        /// <param name="key"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string Format(string key, params object[] args) {
            return string.Format(Text(key), args);
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

            var data = JsonConvert.SerializeObject(Setting, Formatting.Indented);
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
                Setting = JsonConvert.DeserializeObject<Preference>(File.ReadAllText(settingFile));
            }

            // Try auto configure git via registry.
            if (Setting == null || !IsGitConfigured) {
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

            // Apply locales
            if (Setting.UI.Locale != "en_US") {
                foreach (var rs in Current.Resources.MergedDictionaries) {
                    if (rs.Source != null && rs.Source.OriginalString.StartsWith("pack://application:,,,/Resources/Locales/")) {
                        rs.Source = new Uri($"pack://application:,,,/Resources/Locales/{Setting.UI.Locale}.xaml", UriKind.Absolute);
                        break;
                    }
                }
            }

            // Show main window
            if (e.Args.Length == 1) {
                MainWindow = new UI.Launcher(e.Args[0]);
            } else {
                MainWindow = new UI.Launcher(null);
            }
            MainWindow.Show();


            // Check for update.
            if (Setting.CheckUpdate && Setting.LastCheckUpdate != DateTime.Now.DayOfYear) {
                Setting.LastCheckUpdate = DateTime.Now.DayOfYear;
                SaveSetting();
                Task.Run(CheckUpdate);
            }
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

        /// <summary>
        ///     Check for update.
        /// </summary>
        private void CheckUpdate() {
            try {
                var web = new WebClient() { Encoding = Encoding.UTF8 };
                var raw = web.DownloadString("https://gitee.com/api/v5/repos/sourcegit/SourceGit/releases/latest");
                var ver = JsonConvert.DeserializeObject<Git.Version>(raw);
                var cur = Assembly.GetExecutingAssembly().GetName().Version;

                var matches = Regex.Match(ver.TagName, @"^v(\d+)\.(\d+).*");
                if (!matches.Success) return;

                var major = int.Parse(matches.Groups[1].Value);
                var minor = int.Parse(matches.Groups[2].Value);
                if (major > cur.Major || (major == cur.Major && minor > cur.Minor)) {
                    Dispatcher.Invoke(() => {
                        var dialog = new UI.UpdateAvailable(ver);
                        dialog.Owner = MainWindow;
                        dialog.ShowDialog();
                    });
                }
            } catch {
                // IGNORE
            }
        }
    }
}
