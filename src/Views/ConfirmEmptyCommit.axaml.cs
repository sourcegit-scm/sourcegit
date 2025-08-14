using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConfirmEmptyCommit : ChromelessWindow
    {
        public ConfirmEmptyCommit()
        {
            InitializeComponent();
        }

        private async void StageAllThenCommit(object _1, RoutedEventArgs _2)
        {
            if (DataContext is ViewModels.ConfirmEmptyCommit vm)
                await vm.StageAllThenCommitAsync();

            Close();
        }

        private async void Continue(object _1, RoutedEventArgs _2)
        {
            if (DataContext is ViewModels.ConfirmEmptyCommit vm)
                await vm.ContinueAsync();

            Close();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }
    }
}
