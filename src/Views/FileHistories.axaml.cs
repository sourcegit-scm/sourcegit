using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views {
    public partial class FileHistories : Window {
        public FileHistories() {
            InitializeComponent();
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e) {
            if (sender is TextBlock block) {
                var histories = DataContext as ViewModels.FileHistories;
                histories.NavigateToCommit(block.Text);
            }

            e.Handled = true;
        }
    }
}
