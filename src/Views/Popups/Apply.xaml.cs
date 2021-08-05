using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     应用补丁
    /// </summary>
    public partial class Apply : Controls.PopupWidget {
        private string repo = null;
        public string File { get; set; }

        public Apply(string repo) {
            this.repo = repo;
            InitializeComponent();
        }

        public override string GetTitle() {
            return App.Text("Apply.Title");
        }

        public override Task<bool> Start() {
            txtPath.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtPath)) return null;

            var ignoreWS = chkIngoreWS.IsChecked == true;
            var wsMode = (cmbWSOption.SelectedItem as Models.WhitespaceOption).Arg;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                var succ = new Commands.Apply(repo, File, ignoreWS, wsMode).Exec();
                Models.Watcher.SetEnabled(repo, true);
                return succ;
            });
        }

        private void OpenFileBrowser(object sender, System.Windows.RoutedEventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Patch File|*.patch";
            dialog.Title = App.Text("Apply.File.Placeholder");
            dialog.InitialDirectory = repo;
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true) {
                File = dialog.FileName;
                txtPath.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }
    }
}
