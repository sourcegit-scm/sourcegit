using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Create branch dialog
    /// </summary>
    public partial class CreateBranch : UserControl {
        private Git.Repository repo = null;
        private string based = null;

        /// <summary>
        ///     New branch name.
        /// </summary>
        public string BranchName {
            get;
            set;
        }

        /// <summary>
        ///     Auto Stash
        /// </summary>
        public bool AutoStash { get; set; } = false;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        public CreateBranch(Git.Repository opened) {
            InitializeComponent();

            repo = opened;
            nameValidator.Repo = opened;
        }

        /// <summary>
        ///     Create branch based on current head.
        /// </summary>
        /// <param name="repo"></param>
        public static void Show(Git.Repository repo) {
            var current = repo.CurrentBranch();
            if (current != null) Show(repo, current);
        }

        /// <summary>
        ///     Create branch base on existed one.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="branch"></param>
        public static void Show(Git.Repository repo, Git.Branch branch) {
            var dialog = new CreateBranch(repo);
            dialog.based = branch.Name;
            dialog.basedOnType.Data = dialog.FindResource("Icon.Branch") as Geometry;
            dialog.basedOnDesc.Content = branch.Name;

            if (!branch.IsLocal) dialog.txtName.Text = branch.Name.Substring(branch.Remote.Length + 1);

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Show(dialog);
        }

        /// <summary>
        ///     Create branch based on tag.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="tag"></param>
        public static void Show(Git.Repository repo, Git.Tag tag) {
            var dialog = new CreateBranch(repo);
            dialog.based = tag.Name;
            dialog.basedOnType.Data = dialog.FindResource("Icon.Tag") as Geometry;
            dialog.basedOnDesc.Content = tag.Name;

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Show(dialog);
        }

        /// <summary>
        ///     Create branch based on commit.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="commit"></param>
        public static void Show(Git.Repository repo, Git.Commit commit) {
            var dialog = new CreateBranch(repo);
            dialog.based = commit.SHA;
            dialog.basedOnType.Data = dialog.FindResource("Icon.Commit") as Geometry;
            dialog.basedOnDesc.Content = $"{commit.ShortSHA}  {commit.Subject}";

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Show(dialog);
        }

        /// <summary>
        ///     Start create branch.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            txtName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtName)) return;

            var popup = App.Launcher.GetPopupManager(repo);
            popup?.Lock();

            bool checkout = chkCheckout.IsChecked == true;
            await Task.Run(() => {
                if (checkout) {
                    bool stashed = false;

                    if (repo.LocalChanges().Count > 0 && AutoStash) {
                        Git.Stash.Push(repo, true, "CREATE BRANCH AUTO STASH", new List<string>());
                        stashed = true;
                    }

                    repo.Checkout($"-b {BranchName} {based}");

                    if (stashed) {
                        var stashes = repo.Stashes();
                        if (stashes.Count > 0) stashes[0].Pop(repo);
                    }
                } else {
                    Git.Branch.Create(repo, BranchName, based);
                }
            });

            popup?.Close(true);
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
