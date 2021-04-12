using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Interaction logic for DeleteSubmodule.xaml
    /// </summary>
    public partial class DeleteSubmodule : UserControl {
        private Git.Repository repo = null;
        private string submodule = null;

        public DeleteSubmodule(Git.Repository opened, string path) {
            InitializeComponent();
            repo = opened;
            submodule = path;
            targetPath.Text = path;
        }

        public static void Show(Git.Repository repo, string submodule) {
            repo.GetPopupManager()?.Show(new DeleteSubmodule(repo, submodule));
        }

        private async void Sure(object sender, RoutedEventArgs e) {
            var popup = repo.GetPopupManager();
            popup?.Lock();
            await Task.Run(() => repo.DeleteSubmodule(submodule));
            popup?.Close(true);
        }

        private void Cancel(object sender, RoutedEventArgs e) {
            repo.GetPopupManager()?.Close();
        }
    }
}
