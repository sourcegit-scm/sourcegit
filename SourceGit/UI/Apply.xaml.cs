using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.UI {

    /// <summary>
    ///     Apply patch dialog
    /// </summary>
    public partial class Apply : UserControl {
        private Git.Repository repo = null;

        /// <summary>
        ///     Whitespace option.
        /// </summary>
        public class WhitespaceOption {
            public string Name { get; set; }
            public string Desc { get; set; }
            public string Arg { get; set; }

            public WhitespaceOption(string n, string d, string a) {
                Name = n;
                Desc = d;
                Arg = a;
            }
        }

        /// <summary>
        ///     Path of file to be patched.
        /// </summary>
        public string PatchFile { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Apply(Git.Repository opened) {
            repo = opened;
            InitializeComponent();

            combWhitespaceOptions.ItemsSource = new WhitespaceOption[] {
                new WhitespaceOption("No Warn", "Turns off the trailing whitespace warning", "nowarn"),
                new WhitespaceOption("Warn", "Outputs warnings for a few such errors, but applies", "warn"),
                new WhitespaceOption("Error", "Raise errors and refuses to apply the patch", "error"),
                new WhitespaceOption("Error All", "Similar to 'error', but shows more", "error-all"),
            };
            combWhitespaceOptions.SelectedIndex = 0;
        }

        /// <summary>
        ///     Show this dialog.
        /// </summary>
        /// <param name="opened"></param>
        public static void Show(Git.Repository opened) {
            PopupManager.Show(new Apply(opened));
        }

        /// <summary>
        ///     Open file browser dialog for select a file to patch.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FindPatchFile(object sender, RoutedEventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Patch File|*.patch";
            dialog.Title = "Select Patch File";
            dialog.InitialDirectory = repo.Path;
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true) {
                PatchFile = dialog.FileName;
                txtPatchFile.Text = dialog.FileName;
            }
        }

        /// <summary>
        ///     Start apply selected path.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Start(object sender, RoutedEventArgs e) {
            txtPatchFile.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtPatchFile)) return;

            PopupManager.Lock();

            var mode = combWhitespaceOptions.SelectedItem as WhitespaceOption;
            await Task.Run(() => repo.Apply(PatchFile, mode.Arg));

            PopupManager.Close(true);
        }

        /// <summary>
        ///     Cancel options.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel(object sender, RoutedEventArgs e) {
            PopupManager.Close();
        }
    }
}
