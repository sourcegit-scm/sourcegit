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
        public bool HasWindowsTerminal { get; set; }

        public Preference() {
            UpdateGitInfo(false);

            HasWindowsTerminal = Models.ExecutableFinder.Find("wt.exe") != null;
            if (!HasWindowsTerminal) Models.Preference.Instance.General.UseWindowsTerminal = false;

            InitializeComponent();
        }

        private bool UpdateGitInfo(bool updateUi) {
            var isReady = Models.Preference.Instance.IsReady;
            if (isReady) {
                User = new Commands.Config().Get("user.name");
                Email = new Commands.Config().Get("user.email");
                CRLF = new Commands.Config().Get("core.autocrlf");
                Version = new Commands.Version().Query();
                if (string.IsNullOrEmpty(CRLF)) CRLF = "false";
            } else {
                User = "";
                Email = "";
                CRLF = "false";
                Version = "Unknown";
            }
            if (updateUi) {
                editGitUser?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                editGitEmail?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                editGitCrlf?.GetBindingExpression(ComboBox.SelectedValueProperty).UpdateTarget();
                textGitVersion?.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();
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
            var dialog = new Controls.FolderDialog();
            if (dialog.ShowDialog() == true) {
                Models.Preference.Instance.Git.DefaultCloneDir = dialog.SelectedPath;
                txtGitCloneDir?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
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
            }

            Models.Preference.Save();
            Close();
        }
        #endregion
    }
}
