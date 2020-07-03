using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Preference window.
    /// </summary>
    public partial class Preference : UserControl {

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Preference() {
            InitializeComponent();

            int mergeType = App.Preference.MergeTool;
            var merger = Git.MergeTool.Supported[mergeType];
            txtMergePath.IsReadOnly = !merger.IsConfigured;
            txtMergeParam.Text = merger.Parameter;
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
    }
}
