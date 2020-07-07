using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Merge branch dialog.
    /// </summary>
    public partial class Merge : UserControl {
        private Git.Repository repo = null;

        /// <summary>
        ///     Merge option.
        /// </summary>
        public class Option {
            public string Name { get; set; }
            public string Desc { get; set; }
            public string Arg { get; set; }

            public Option(string n, string d, string a) {
                Name = n;
                Desc = d;
                Arg = a;
            }
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened">Opened repository</param>
        /// <param name="source">Source branch to merge data from.</param>
        /// <param name="dest">Target branch to merge into</param>
        public Merge(Git.Repository opened, string source, string dest) {
            InitializeComponent();

            repo = opened;
            sourceBranch.Content = source;
            targetBranch.Content = dest;
            combOptions.ItemsSource = new Option[] {
                new Option("Default", "Fast-forward if possible", ""),
                new Option("No Fast-forward", "Always create a merge commit", "--no-ff"),
                new Option("Squash", "Use '--squash'", "--squash"),
                new Option("Don't commit", "Merge without commit", "--no-commit"),
            };
            combOptions.SelectedIndex = 0;
        }

        /// <summary>
        ///     Display this dialog.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        public static void Show(Git.Repository opened, string source, string dest) {
            PopupManager.Show(new Merge(opened, source, dest));
        }

        /// <summary>
        ///     Start merge directly(Fast-forward).
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        public static void StartDirectly(Git.Repository opened, string source, string dest) {
            var merge = new Merge(opened, source, dest);
            PopupManager.Show(merge);
            PopupManager.Lock();

            Task.Run(() => {
                opened.Merge(source, "");
                merge.Dispatcher.Invoke(() => {
                    PopupManager.Close(true);
                });
            });
        }

        /// <summary>
        ///     Start merge
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            PopupManager.Lock();

            var branch = sourceBranch.Content as string;
            var opt = combOptions.SelectedItem as Option;
            await Task.Run(() => repo.Merge(branch, opt.Arg));

            PopupManager.Close(true);
        }

        /// <summary>
        ///     Cancel merge.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
    }
}
