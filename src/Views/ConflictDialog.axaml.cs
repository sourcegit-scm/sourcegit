using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConflictDialog : ChromelessWindow
    {
        public ConflictDialog()
        {
            InitializeComponent();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
