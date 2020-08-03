using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Create tag dialog
    /// </summary>
    public partial class CreateTag : UserControl {
        private Git.Repository repo = null;
        private string based = null;

        /// <summary>
        ///     Tag name
        /// </summary>
        public string TagName { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="repo"></param>
        public CreateTag(Git.Repository opened) {
            InitializeComponent();

            repo = opened;
            nameValidator.Repo = opened;
        }

        /// <summary>
        ///     Create tag using current branch.
        /// </summary>
        /// <param name="repo">Opened repository.</param>
        public static void Show(Git.Repository repo) {
            Show(repo, repo.Branches().First(b => b.IsCurrent));
        }

        /// <summary>
        ///     Create tag using branch
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="branch"></param>
        public static void Show(Git.Repository repo, Git.Branch branch) {
            if (branch == null) {
                App.RaiseError("Empty repository!");
                return;
            }

            var dialog = new CreateTag(repo);
            dialog.based = branch.Head;
            dialog.basedOnType.Data = dialog.FindResource("Icon.Branch") as Geometry;
            dialog.basedOnDesc.Content = branch.Name;

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Show(dialog);
        }

        /// <summary>
        ///     Create tag using commit.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="commit"></param>
        public static void Show(Git.Repository repo, Git.Commit commit) {
            var dialog = new CreateTag(repo);
            dialog.based = commit.SHA;
            dialog.basedOnType.Data = dialog.FindResource("Icon.Commit") as Geometry;
            dialog.basedOnDesc.Content = $"{commit.ShortSHA}  {commit.Subject}";

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Show(dialog);
        }

        /// <summary>
        ///     Start to create tag.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start(object sender, RoutedEventArgs e) {
            tagName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(tagName)) return;

            Git.Tag.Add(repo, TagName, based, tagMessage.Text);

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Close();
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            App.Launcher.GetPopupManager(repo)?.Close();
        }
    }
}
