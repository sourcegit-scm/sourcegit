using Microsoft.Win32;
using System;
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
        public string MergeCmd { get; set; }

        private string locale;
        private string avatarServer;
        private bool useDarkTheme;
        private bool checkUpdate;
        private bool autoFetch;

        public Preference() {
            if (Models.Preference.Instance.IsReady) {
                User = new Commands.Config().Get("user.name");
                Email = new Commands.Config().Get("user.email");
                CRLF = new Commands.Config().Get("core.autocrlf");
                if (string.IsNullOrEmpty(CRLF)) CRLF = "false";
            } else {
                User = "";
                Email = "";
                CRLF = "false";
            }

            var merger = Models.MergeTool.Supported.Find(x => x.Type == Models.Preference.Instance.MergeTool.Type);
            if (merger != null) MergeCmd = merger.Cmd;

            locale = Models.Preference.Instance.General.Locale;
            avatarServer = Models.Preference.Instance.General.AvatarServer;
            useDarkTheme = Models.Preference.Instance.General.UseDarkTheme;
            checkUpdate = Models.Preference.Instance.General.CheckForUpdate;
            autoFetch = Models.Preference.Instance.General.AutoFetchRemotes;

            InitializeComponent();
        }

        #region EVENTS
        private void SelectGitPath(object sender, RoutedEventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Git Executable|git.exe";
            dialog.FileName = "git.exe";
            dialog.Title = App.Text("Preference.Dialog.GitExe");
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true) {
                Models.Preference.Instance.Git.Path = dialog.FileName;
                editGitPath?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }

        private void SelectGitCloneDir(object sender, RoutedEventArgs e) {
            var dialog = new Controls.FolderDialog("Preference.Dialog.GitDir");
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

            MergeCmd = tool.Cmd;
            txtMergeCmd?.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();

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

            var general = Models.Preference.Instance.General;
            if (locale != general.Locale ||
                avatarServer != general.AvatarServer ||
                useDarkTheme != general.UseDarkTheme ||
                checkUpdate != general.CheckForUpdate ||
                autoFetch != general.AutoFetchRemotes) {
                var result = MessageBox.Show(
                    this,
                    App.Text("Restart.Content"),
                    App.Text("Restart.Title"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Cancel);
                if (result == MessageBoxResult.OK) {
                    App.Restart();
                } else {
                    Close();
                }
            } else {
                Close();
            }
        }
        #endregion
    }
}
