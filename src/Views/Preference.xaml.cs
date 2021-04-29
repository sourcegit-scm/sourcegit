using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views {

    /// <summary>
    ///     设置面板
    /// </summary>
    public partial class Preference : Window {

        public string User { get; set; }
        public string Email { get; set; }
        public string CRLF { get; set; }
        public string MergeCmd { get; set; }

        public Preference() {
            User = new Commands.Config().Get("user.name");
            Email = new Commands.Config().Get("user.email");
            CRLF = new Commands.Config().Get("core.autocrlf");
            if (string.IsNullOrEmpty(CRLF)) CRLF = "false";

            var merger = Models.MergeTool.Supported.Find(x => x.Type == Models.Preference.Instance.MergeTool.Type);
            if (merger != null) MergeCmd = merger.Cmd;

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
            FolderBrowser.Open(this, App.Text("Preference.Dialog.GitDir"), path => {
                Models.Preference.Instance.Git.DefaultCloneDir = path;
                txtGitCloneDir?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            });
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
            var selector = sender as ComboBox;
            var type = (int)selector.SelectedValue;
            var tool = Models.MergeTool.Supported.Find(x => x.Type == type);
            if (tool == null) return;

            Models.Preference.Instance.MergeTool.Path = tool.Finder();
            MergeCmd = tool.Cmd;

            txtMergeExec?.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            txtMergeCmd?.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();

            e.Handled = true;
        }

        private void Quit(object sender, RoutedEventArgs e) {
            var cmd = new Commands.Config();
            var oldUser = cmd.Get("user.name");
            if (oldUser != User) cmd.Set("user.name", User);

            var oldEmail = cmd.Get("user.email");
            if (oldEmail != Email) cmd.Set("user.email", Email);

            var oldCRLF = cmd.Get("core.autocrlf");
            if (oldCRLF != CRLF) cmd.Set("core.autocrlf", CRLF);

            Models.Preference.Save();
            Close();
        }
        #endregion
    }
}
