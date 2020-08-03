using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Confirm to discard changes dialog.
    /// </summary>
    public partial class Discard : UserControl {
        private Git.Repository repo = null;
        private List<Git.Change> changes = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="targets"></param>
        public Discard(Git.Repository opened, List<Git.Change> targets) {
            repo = opened;
            changes = targets;

            InitializeComponent();

            if (changes == null || changes.Count == 0) {
                txtPath.Content = "All local changes in working copy.";
                icon.Data = FindResource("Icon.Folder") as Geometry;
            } else if (changes.Count == 1) {
                txtPath.Content = changes[0].Path;
            } else {
                txtPath.Content = $"Total {changes.Count} changes ...";
            }
        }

        /// <summary>
        ///     Show this dialog
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="targets"></param>
        public static void Show(Git.Repository opened, List<Git.Change> targets) {
            var popup = App.Launcher.GetPopupManager(opened);
            popup?.Show(new Discard(opened, targets));
        }

        private async void Sure(object sender, RoutedEventArgs e) {
            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Lock();
            await Task.Run(() => repo.Discard(changes));
            popup?.Close(true);
        }

        private void Cancel(object sender, RoutedEventArgs e) {
            App.Launcher.GetPopupManager(repo)?.Close();
        }
    }
}
