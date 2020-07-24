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
        /// <param name="job"></param>
        public static void Show(Action job) {
            var dialog = new Waiting();
            PopupManager.Show(dialog);
            PopupManager.Lock();
            Task.Run(() => {
                job.Invoke();
                dialog.Dispatcher.Invoke(() => {
                    PopupManager.Close(true);
                });
            });
        }
    }
}
