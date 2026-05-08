using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConfirmEmptyCommit : ChromelessWindow
    {
        public ConfirmEmptyCommit()
        {
            InitializeComponent();
        }

        private void StageSelectedThenCommit(object _1, RoutedEventArgs _2)
        {
            Close(Models.ConfirmEmptyCommitResult.StageSelectedAndCommit);
        }

        private void StageAllThenCommit(object _1, RoutedEventArgs _2)
        {
            Close(Models.ConfirmEmptyCommitResult.StageAllAndCommit);
        }

        private void Continue(object _1, RoutedEventArgs _2)
        {
            Close(Models.ConfirmEmptyCommitResult.CreateEmptyCommit);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close(Models.ConfirmEmptyCommitResult.Cancel);
        }
    }
}
