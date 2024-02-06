using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views {
    public partial class AssumeUnchangedManager : Window {
        public AssumeUnchangedManager() {
            InitializeComponent();
        }

        private void CloseWindow(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
