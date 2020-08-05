using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     `git init` confirm panel.
    /// </summary>
    public partial class Init : UserControl {
        private string workingDir = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="path"></param>
        public Init(string path) {
            workingDir = path;
            InitializeComponent();
            txtPath.Content = path;
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="path"></param>
        public static void Show(string path) {
            var popup = App.GetPopupManager(null);
            popup.Show(new Init(path));
        }

        /// <summary>
        ///     Do `git init`
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            var popup = App.GetPopupManager(null);
            popup.Lock();

            await Task.Run(() => {
                var errs = Git.Repository.RunCommand(workingDir, "init -q", null);
                if (errs != null) {
                    App.RaiseError(errs);
                } else {
                    App.Preference.AddRepository(workingDir, "");
                }
            });

            popup.Close(true);

            var repo = App.Preference.FindRepository(workingDir);
            if (repo != null) repo.Open();
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            App.GetPopupManager(null).Close();
        }
    }
}
