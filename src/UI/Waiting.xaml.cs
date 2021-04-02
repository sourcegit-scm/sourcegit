using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     General waiting dialog.
    /// </summary>
    public partial class Waiting : UserControl {

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Waiting() {
            InitializeComponent();
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="tip"></param>
        /// <param name="job"></param>
        public static void Show(Git.Repository repo, string tipKey, Action job) {
            var dialog = new Waiting();
            var tip = dialog.FindResource(tipKey) as string;
            if (tip != null) dialog.txtTip.Text = tip;

            var popup = repo.GetPopupManager();
            popup?.Show(dialog);
            popup?.Lock();
            Task.Run(() => {
                job.Invoke();
                dialog.Dispatcher.Invoke(() => {
                    popup?.Close(true);
                });
            });
        }
    }
}
