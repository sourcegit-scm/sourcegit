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
        /// <param name="job"></param>
        public static void Show(Git.Repository repo, Action job) {
            var dialog = new Waiting();
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
