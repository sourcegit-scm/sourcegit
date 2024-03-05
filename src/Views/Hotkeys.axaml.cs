using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views {
    public partial class Hotkeys : Window {
        public Hotkeys() {
            InitializeComponent();
        }

        private void CloseWindow(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
