using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     `git init` confirm panel.
    /// </summary>
    public partial class Init : UserControl {
        private PopupManager popup = null;
        private string workingDir = null;
        private Action onSuccess = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="mgr"></param>
        /// <param name="path"></param>
        public Init(PopupManager mgr, string path, Action success) {
            popup = mgr;
            workingDir = path;
            onSuccess = success;
            InitializeComponent();
            txtPath.Content = path;
        }

        /// <summary>
        ///     Do `git init`
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            popup.Lock();

            await Task.Run(() => {
                var errs = Git.Repository.RunCommand(workingDir, "init -q", null);
                if (errs != null) {
                    App.RaiseError(errs);
                } else {
                    App.Setting.AddRepository(workingDir, "");
                }
            });

            var repo = App.Setting.FindRepository(workingDir);
            if (repo != null) {
                App.Open(repo);
                onSuccess?.Invoke();
            }

            popup.Close(true);
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            popup.Close();
        }
    }
}
