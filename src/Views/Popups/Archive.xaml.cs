using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     存档操作面板
    /// </summary>
    public partial class Archive : Controls.PopupWidget {
        private string repo;
        private string revision;

        public string SaveTo { get; set; } = "archive.zip";

        public Archive(string repo, Models.Commit revision) {
            this.repo = repo;
            this.revision = revision.SHA;

            InitializeComponent();

            txtBased.Text = $"{revision.ShortSHA}  {revision.Subject}";
        }

        public override string GetTitle() {
            return App.Text("Archive");
        }

        public override Task<bool> Start() {
            txtSaveTo.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtSaveTo)) return null;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                var succ = new Commands.Archive(repo, revision, SaveTo, UpdateProgress).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return succ;
            });
        }

        private void OpenFileBrowser(object sender, RoutedEventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Filter = "ZIP|*.zip";
            dialog.Title = App.Text("Archive.File");
            dialog.InitialDirectory = repo;
            dialog.CheckFileExists = false;

            if (dialog.ShowDialog() == true) {
                SaveTo = dialog.FileName;
                txtSaveTo.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }
    }
}
