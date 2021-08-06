using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     存档操作面板
    /// </summary>
    public partial class Archive : Controls.PopupWidget {
        private string repo;
        private string revision;

        public string SaveTo { get; set; }

        public Archive(string repo, Models.Branch branch) {
            this.repo = repo;
            this.revision = branch.Head;
            this.SaveTo = $"archive-{Path.GetFileNameWithoutExtension(branch.Name)}.zip";

            InitializeComponent();

            iconBased.Data = FindResource("Icon.Branch") as Geometry;
            txtBased.Text = branch.IsLocal ? branch.Name : $"{branch.Remote}/{branch.Name}";
        }

        public Archive(string repo, Models.Commit revision) {
            this.repo = repo;
            this.revision = revision.SHA;
            this.SaveTo = $"archive-{revision.ShortSHA}.zip";

            InitializeComponent();

            iconBased.Data = FindResource("Icon.Commit") as Geometry;
            txtSHA.Text = revision.ShortSHA;
            badgeSHA.Visibility = Visibility.Visible;
            txtBased.Text = revision.Subject;
        }

        public Archive(string repo, Models.Tag tag) {
            this.repo = repo;
            this.revision = tag.SHA;
            this.SaveTo = $"archive-{tag.Name}.zip";

            InitializeComponent();

            iconBased.Data = FindResource("Icon.Tag") as Geometry;
            txtBased.Text = tag.Name;
        }

        public override string GetTitle() {
            return App.Text("Archive.Title");
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
            dialog.Title = App.Text("Archive.File.Placeholder");
            dialog.InitialDirectory = repo;
            dialog.CheckFileExists = false;

            if (dialog.ShowDialog() == true) {
                SaveTo = dialog.FileName;
                txtSaveTo.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }
    }
}
