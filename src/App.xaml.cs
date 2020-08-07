using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace SourceGit {

    /// <summary>
    ///     Application.
    /// </summary>
    public partial class App : Application {

        /// <summary>
        ///     Getter/Setter for Git preference.
        /// </summary>
        public static Git.Preference Preference {
            get { return Git.Preference.Instance; }
            set { Git.Preference.Instance = value; }
        }

        /// <summary>
        ///     Check if GIT has been configured.
        /// </summary>
        public static bool IsGitConfigured {
            get {
                return !string.IsNullOrEmpty(Preference.GitExecutable)
                    && File.Exists(Preference.GitExecutable);
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
        ///     Startup event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAppStartup(object sender, StartupEventArgs e) {
            // Use this app as a sequence editor?
            var args = e.Args;
            if (args.Length > 1) {
                if (args[0] == "--interactive-rebase") {
                    if (args.Length < 3) {
                        Environment.Exit(1);
                        return;
                    }

                    File.WriteAllText(args[2], File.ReadAllText(args[1]));
                }

                Environment.Exit(0);
                return;
            }

            // Try auto configure git via registry.
            if (!IsGitConfigured) {
                var root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

                var git = root.OpenSubKey("SOFTWARE\\GitForWindows");
                if (git != null) {
                    Preference.GitExecutable = Path.Combine(
                        git.GetValue("InstallPath") as string, 
                        "bin", 
                        "git.exe");
                }
            }

            // Apply themes
            if (Preference.UIUseLightTheme) {
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
            Git.Preference.Save();
            GC.Collect();
        }
    }
}
