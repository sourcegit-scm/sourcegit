using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Clone dialog.
    /// </summary>
    public partial class Clone : UserControl {

        /// <summary>
        ///     Remote repository
        /// </summary>
        public string RemoteUri { get; set; }

        /// <summary>
        ///     Parent folder.
        /// </summary>
        public string ParentFolder { get; set; }

        /// <summary>
        ///     Local name.
        /// </summary>
        public string LocalName { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Clone() {
            ParentFolder = App.Preference.GitDefaultCloneDir;
            InitializeComponent();
        }

        /// <summary>
        ///     Show clone dialog.
        /// </summary>
        public static void Show() {
            PopupManager.Show(new Clone());
        }

        /// <summary>
        ///     Select parent folder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectParentFolder(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Git Repository URL";
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                txtParentFolder.Text = dialog.SelectedPath;
            }
        }

        /// <summary>
        ///     Start clone
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start(object sender, RoutedEventArgs e) {
            txtUrl.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtUrl)) return;

            txtParentFolder.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtParentFolder)) return;

            string repoName;
            if (string.IsNullOrWhiteSpace(LocalName)) {
                var from = RemoteUri.LastIndexOfAny(new char[] { '\\', '/' });
                if (from <= 0) return;

                var name = RemoteUri.Substring(from + 1);
                repoName = name.Replace(".git", "");
            } else {
                repoName = LocalName;
            }

            PopupManager.Lock();

            status.Visibility = Visibility.Visible;
            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);

            Task.Run(() => {
                var repo = Git.Repository.Clone(RemoteUri, ParentFolder, repoName, msg => Dispatcher.Invoke(() => statusMsg.Content = msg));
                if (repo == null) {
                    PopupManager.Unlock();
                    Dispatcher.Invoke(() => {
                        status.Visibility = Visibility.Collapsed;
                        statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                    });
                } else {
                    Dispatcher.Invoke(() => PopupManager.Close(true));
                    repo.Open();
                }
            });
        }

        /// <summary>
        ///     Cancel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
    }
}
