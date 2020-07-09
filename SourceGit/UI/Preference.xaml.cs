using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Preference window.
    /// </summary>
    public partial class Preference : UserControl {

        /// <summary>
        ///     Git global user name.
        /// </summary>
        public string GlobalUser {
            get;
            set;
        }

        /// <summary>
        ///     Git global user email.
        /// </summary>
        public string GlobalUserEmail {
            get;
            set;
        }

        /// <summary>
        ///     Git core.autocrlf setting.
        /// </summary>
        public string AutoCRLF {
            get;
            set;
        }

        /// <summary>
        ///     Options for core.autocrlf
        /// </summary>
        public class AutoCRLFOption {
            public string Value { get; set; }
            public string Desc { get; set; }
            
            public AutoCRLFOption(string v, string d) {
                Value = v;
                Desc = d;
            }
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Preference() {
            GlobalUser = GetConfig("user.name");
            GlobalUserEmail = GetConfig("user.email");
            AutoCRLF = GetConfig("core.autocrlf");
            if (string.IsNullOrEmpty(AutoCRLF)) AutoCRLF = "false";

            InitializeComponent();

            int mergeType = App.Preference.MergeTool;
            var merger = Git.MergeTool.Supported[mergeType];
            txtMergePath.IsReadOnly = !merger.IsConfigured;
            txtMergeParam.Text = merger.Parameter;

            var crlfOptions = new List<AutoCRLFOption>() {
                new AutoCRLFOption("true", "Commit as LF, checkout as CRLF"),
                new AutoCRLFOption("input", "Only convert for commit"),
                new AutoCRLFOption("false", "Do NOT convert"),
            };
            cmbAutoCRLF.ItemsSource = crlfOptions;
            cmbAutoCRLF.SelectedItem = crlfOptions.Find(o => o.Value == AutoCRLF);
        }

        /// <summary>
        ///     Show preference.
        /// </summary>
        public static void Show() {
            PopupManager.Show(new Preference());
        }

        /// <summary>
        ///     Close this dialog
        /// </summary>
        private void Close(object sender, RoutedEventArgs e) {
            var oldUser = GetConfig("user.name");
            if (oldUser != GlobalUser) SetConfig("user.name", GlobalUser);

            var oldEmail = GetConfig("user.email");
            if (oldEmail != GlobalUserEmail) SetConfig("user.email", GlobalUserEmail);

            var oldAutoCRLF = GetConfig("core.autocrlf");
            if (oldAutoCRLF != AutoCRLF) SetConfig("core.autocrlf", AutoCRLF);

            PopupManager.Close();
        }

        /// <summary>
        ///     Select git executable file path.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectGitPath(object sender, RoutedEventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Git Executable|git.exe";
            dialog.FileName = "git.exe";
            dialog.Title = "Select Git Executable File";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true) {
                txtGitPath.Text = dialog.FileName;
                App.Preference.GitExecutable = dialog.FileName;
            }
        }

        /// <summary>
        ///     Set default clone path.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectDefaultClonePath(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select Folder To Clone Repository Into As Default";
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                txtGitCloneDir.Text = dialog.SelectedPath;
                App.Preference.GitDefaultCloneDir = dialog.SelectedPath;
            }
        }

        /// <summary>
        ///     Choose external merge tool.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeMergeTool(object sender, SelectionChangedEventArgs e) {
            if (IsLoaded) {
                var t = Git.MergeTool.Supported[App.Preference.MergeTool];

                App.Preference.MergeExecutable = t.Finder();

                txtMergePath.Text = App.Preference.MergeExecutable;
                txtMergeParam.Text = t.Parameter;
                txtMergePath.IsReadOnly = !t.IsConfigured;
            }
        }

        /// <summary>
        ///     Set merge tool executable file path.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectMergeToolPath(object sender, RoutedEventArgs e) {
            int mergeType = App.Preference.MergeTool;
            if (mergeType == 0) return;

            var merger = Git.MergeTool.Supported[mergeType];
            var dialog = new OpenFileDialog();
            dialog.Filter = $"{merger.Name} Executable|{merger.ExecutableName}";
            dialog.Title = $"Select {merger.Name} Install Path";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true) {
                txtMergePath.Text = dialog.FileName;
                App.Preference.MergeExecutable = dialog.FileName;
            }
        }

        /// <summary>
        ///     Set core.autocrlf
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoCRLFSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var mode = e.AddedItems[0] as AutoCRLFOption;
            if (mode == null) return;

            AutoCRLF = mode.Value;
        }

        #region CONFIG
        private string GetConfig(string key) {
            if (!App.IsGitConfigured) return "";

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = App.Preference.GitExecutable;
            startInfo.Arguments = $"config --global {key}";
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
            startInfo.Arguments = $"config --global {key} \"{val}\"";
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
