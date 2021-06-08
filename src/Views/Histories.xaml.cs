using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SourceGit.Views {

    /// <summary>
    ///     文件历史
    /// </summary>
    public partial class Histories : Window {
        private string repo = null;
        private string file = null;
        private bool isLFSEnabled = false;

        public Histories(string repo, string file) {
            this.repo = repo;
            this.file = file;
            this.isLFSEnabled = new Commands.LFS(repo).IsFiltered(file);

            InitializeComponent();

            Task.Run(() => {
                var commits = new Commands.Commits(repo, $"-n 10000 -- \"{file}\"").Result();
                Dispatcher.Invoke(() => {
                    loading.IsAnimating = false;
                    loading.Visibility = Visibility.Collapsed;
                    commitList.ItemsSource = commits;
                    commitList.SelectedIndex = 0;
                });
            });
        }

        #region WINDOW_COMMANDS
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
        #endregion

        #region EVENTS
        private void OnCommitSelectedChanged(object sender, SelectedCellsChangedEventArgs e) {
            var commit = (sender as DataGrid).SelectedItem as Models.Commit;
            if (commit == null) return;

            var start = $"{commit.SHA}^";
            if (commit.Parents.Count == 0) start = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

            diffViewer.Diff(repo, new Widgets.DiffViewer.Option() {
                RevisionRange = new string[] { start, commit.SHA },
                Path = file,
                UseLFS = isLFSEnabled,
            });
        }

        private void GotoCommit(object sender, RequestNavigateEventArgs e) {
            Models.Watcher.Get(repo).NavigateTo(e.Uri.OriginalString);
            e.Handled = true;
        }
        #endregion
    }
}
