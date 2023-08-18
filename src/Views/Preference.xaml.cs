using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views {

    /// <summary>
    ///     设置面板
    /// </summary>
    public partial class Preference : Controls.Window {

        public string User { get; set; }
        public string Email { get; set; }
        public string CRLF { get; set; }
        public string Version { get; set; }
        public string GPGExec { get; set; }
        public bool GPGSigningEnabled { get; set; }
        public string GPGUserSigningKey { get; set; }

        public Preference() {
            UpdateGitInfo(false);
            InitializeComponent();
        }

        private bool UpdateGitInfo(bool updateUi) {
            var isReady = Models.Preference.Instance.IsReady;
            if (isReady) {
                var cmd = new Commands.Config();
                User = cmd.Get("user.name");
                Email = cmd.Get("user.email");
                CRLF = cmd.Get("core.autocrlf");
                Version = new Commands.Version().Query();
                if (string.IsNullOrEmpty(CRLF)) CRLF = "false";
                GPGExec = cmd.Get("gpg.program");
                if (string.IsNullOrEmpty(GPGExec)) {
                    string gitInstallFolder = Path.GetDirectoryName(Models.Preference.Instance.Git.Path);
                    string defaultGPG = Path.GetFullPath(gitInstallFolder + "/../usr/bin/gpg.exe");
                    if (File.Exists(defaultGPG)) GPGExec = defaultGPG;
                }
                GPGSigningEnabled = cmd.Get("commit.gpgsign") == "true";
                GPGUserSigningKey = cmd.Get("user.signingkey");
            } else {
                User = "";
                Email = "";
                CRLF = "false";
                Version = "Unknown";
                GPGExec = "";
                GPGSigningEnabled = false;
                GPGUserSigningKey = "";
            }
            if (updateUi) {
                editGitUser?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                editGitEmail?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                editGitCrlf?.GetBindingExpression(ComboBox.SelectedValueProperty).UpdateTarget();
                textGitVersion?.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();
                txtGPGExec?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
            return isReady;
        }

        #region EVENTS
        private void LocaleChanged(object sender, SelectionChangedEventArgs e) {
            Models.Locale.Change();
        }

        private void ChangeTheme(object sender, RoutedEventArgs e) {
            Models.Theme.Change();
        }

        private void SelectGitPath(object sender, RoutedEventArgs e) {
            var initDir = Models.ExecutableFinder.Find("git.exe");
            if (initDir == null) initDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            else initDir = Path.GetDirectoryName(initDir);

            var dialog = new OpenFileDialog {
                Filter = "Git Executable|git.exe",
                FileName = "git.exe",
                Title = App.Text("Preference.Dialog.GitExe"),
                InitialDirectory = initDir,
                CheckFileExists = true,
            };

            if (dialog.ShowDialog() == true) {
                Models.Preference.Instance.Git.Path = dialog.FileName;
                editGitPath?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                UpdateGitInfo(true);
            }
        }

        private void SelectGitCloneDir(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Models.Preference.Instance.Git.DefaultCloneDir = dialog.SelectedPath;
                txtGitCloneDir?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }

        private void SelectGPGExec(object sender, RoutedEventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Filter = $"GPG Executable|gpg.exe";
            dialog.Title = App.Text("GPG.Path.Placeholder");
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true) {
                GPGExec = dialog.FileName;
                txtGPGExec?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }

        private void SelectMergeTool(object sender, RoutedEventArgs e) {
            var type = Models.Preference.Instance.MergeTool.Type;
            var tool = Models.MergeTool.Supported.Find(x => x.Type == type);

            if (tool == null || tool.Type == 0) return;

            var dialog = new OpenFileDialog();
            dialog.Filter = $"{tool.Name} Executable|{tool.Exec}";
            dialog.Title = App.Text("Preference.Dialog.Merger", tool.Name);
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true) {
                Models.Preference.Instance.MergeTool.Path = dialog.FileName;
                txtMergeExec?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }

        private void MergeToolChanged(object sender, SelectionChangedEventArgs e) {
            var type = (int)(sender as ComboBox).SelectedValue;
            var tool = Models.MergeTool.Supported.Find(x => x.Type == type);
            if (tool == null) return;

            if (IsLoaded) {
                Models.Preference.Instance.MergeTool.Path = tool.Finder();
                txtMergeExec?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }

            e.Handled = true;
        }

        private void Quit(object sender, RoutedEventArgs e) {
            if (Models.Preference.Instance.IsReady) {
                var cmd = new Commands.Config();
                var oldUser = cmd.Get("user.name");
                if (oldUser != User) cmd.Set("user.name", User);

                var oldEmail = cmd.Get("user.email");
                if (oldEmail != Email) cmd.Set("user.email", Email);

                var oldCRLF = cmd.Get("core.autocrlf");
                if (oldCRLF != CRLF) cmd.Set("core.autocrlf", CRLF);

                var oldGPGExec = cmd.Get("gpg.program");
                if (oldGPGExec != GPGExec) cmd.Set("gpg.program", GPGExec);

                var oldGPGSigningEnabledStr = cmd.Get("commit.gpgsign");
                var oldGPGSigningEnabled = "true" == oldGPGSigningEnabledStr;
                if (oldGPGSigningEnabled != GPGSigningEnabled) cmd.Set("commit.gpgsign", GPGSigningEnabled ? "true" : "false");

                var oldGPGUserSigningKey = cmd.Get("user.signingkey");
                if (oldGPGUserSigningKey != GPGUserSigningKey) cmd.Set("user.signingkey", GPGUserSigningKey);
            }

            Models.Preference.Save();
            Close();
        }
        #endregion
    }
}
