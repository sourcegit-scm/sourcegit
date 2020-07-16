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
        ///     Interactive rebase sequence file.
        /// </summary>
        public static string InteractiveRebaseScript {
            get;
            private set;
        }

        /// <summary>
        ///     TODO file for interactive rebase.
        /// </summary>
        public static string InteractiveRebaseTodo {
            get;
            private set;
        }

        /// <summary>
        ///     Error handler.
        /// </summary>
        public static Action<string> OnError {
            get;
            set;
        }

        /// <summary>
        ///     Raise error message.
        /// </summary>
        /// <param name="message"></param>
        public static void RaiseError(string message) {
            OnError?.Invoke(message);
        }

        /// <summary>
        ///     Startup event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAppStartup(object sender, StartupEventArgs e) {
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

            // Files for interactive rebase.
            InteractiveRebaseScript = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SourceGit",
                "rebase.bat");
            InteractiveRebaseTodo = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SourceGit",
                "REBASE_TODO");
            if (!File.Exists(InteractiveRebaseScript)) {
                var folder = Path.GetDirectoryName(InteractiveRebaseScript);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                File.WriteAllText(InteractiveRebaseScript, $"@echo off\ntype \"{InteractiveRebaseTodo}\" > .git\\rebase-merge\\git-rebase-todo");
                File.WriteAllText(InteractiveRebaseTodo, "");
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
            var launcher = new UI.Launcher();
            launcher.Show();
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
