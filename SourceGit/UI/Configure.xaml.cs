using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Repository configuration dialog
    /// </summary>
    public partial class Configure : UserControl {
        private Git.Repository repo = null;

        /// <summary>
        ///     User name for this repository.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        ///     User email for this repository.
        /// </summary>
        public string UserEmail { get; set; }

        /// <summary>
        ///     Commit template for this repository.
        /// </summary>
        public string CommitTemplate { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="repo"></param>
        public Configure(Git.Repository repo) {
            this.repo = repo;

            UserName = GetConfig("user.name");
            UserEmail = GetConfig("user.email");
            CommitTemplate = repo.CommitTemplate;

            InitializeComponent();
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="repo"></param>
        public static void Show(Git.Repository repo) {
            PopupManager.Show(new Configure(repo));
        }

        #region EVENTS
        private void Save(object sender, RoutedEventArgs e) {
            var oldUser = GetConfig("user.name");
            if (oldUser != UserName) SetConfig("user.name", UserName);

            var oldEmail = GetConfig("user.email");
            if (oldEmail != UserEmail) SetConfig("user.email", UserEmail);

            if (CommitTemplate != repo.CommitTemplate) {
                repo.CommitTemplate = CommitTemplate;
                Git.Preference.Save();
            }

            Close(sender, e);
        }

        private void Close(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
        #endregion

        #region CONFIGURE
        private string GetConfig(string key) {
            if (!App.IsGitConfigured) return "";

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = App.Preference.GitExecutable;
            startInfo.Arguments = $"config {key}";
            startInfo.WorkingDirectory = repo.Path;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;

            var proc = new Process() { StartInfo = startInfo };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            proc.Close();

            return output.Trim();
        }

        private void SetConfig(string key, string val) {
            if (!App.IsGitConfigured) return;

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = App.Preference.GitExecutable;
            startInfo.Arguments = $"config {key} \"{val}\"";
            startInfo.WorkingDirectory = repo.Path;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            var proc = new Process() { StartInfo = startInfo };
            proc.Start();
            proc.WaitForExit();
            proc.Close();
        }
        #endregion
    }
}
