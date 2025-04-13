using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConfirmCommit : ChromelessWindow
    {
        public ConfirmCommit()
        {
            InitializeComponent();
        }

        private void Sure(object _1, RoutedEventArgs _2)
        {
            (DataContext as ViewModels.ConfirmCommit)?.Continue();
            Close();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }
    }
}
