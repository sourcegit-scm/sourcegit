using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Create or edit remote dialog.
    /// </summary>
    public partial class Remote : UserControl {
        private Git.Repository repo = null;
        private Git.Remote remote = null;

        public string RemoteName { get; set; }
        public string RemoteUri { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="editing">Editing remote</param>
        public Remote(Git.Repository opened, Git.Remote editing) {
            repo = opened;
            remote = editing;

            if (remote != null) {
                RemoteName = remote.Name;
                RemoteUri = remote.URL;
            }

            InitializeComponent();
            nameValidator.Repo = repo;

            if (remote != null) {
                title.Content = "Edit Remote";
            } else {
                title.Content = "Add New Remote";
            }
        }

        /// <summary>
        ///     Display this dialog.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="editing"></param>
        public static void Show(Git.Repository opened, Git.Remote editing = null) {
            PopupManager.Show(new Remote(opened, editing));
        }

        /// <summary>
        ///     Commit request.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Sure(object sender, RoutedEventArgs e) {
            txtName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtName)) return;

            txtUrl.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtUrl)) return;

            PopupManager.Lock();

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            status.Visibility = Visibility.Visible;

            await Task.Run(() => {
                if (remote != null) {
                    remote.Edit(repo, RemoteName, RemoteUri);
                } else {
                    Git.Remote.Add(repo, RemoteName, RemoteUri);
                }
            });

            status.Visibility = Visibility.Collapsed;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            PopupManager.Close(true);
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
