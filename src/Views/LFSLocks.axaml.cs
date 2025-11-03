using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class LFSLocks : ChromelessWindow
    {
        public LFSLocks()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private async void OnUnlockButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LFSLocks vm && sender is Button button)
                await vm.UnlockAsync(button.DataContext as Models.LFSLock, false);

            e.Handled = true;
        }

        private async void OnForceUnlockButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LFSLocks vm && sender is Button button)
                await vm.UnlockAsync(button.DataContext as Models.LFSLock, true);

            e.Handled = true;
        }

        private async void OnUnlockAllMyLocksButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.LFSLocks vm)
                return;

            var shouldContinue = await App.AskConfirmAsync(App.Text("GitLFS.Locks.UnlockAllMyLocks.Confirm"));
            if (!shouldContinue)
                return;

            await vm.UnlockAllMyLocksAsync();
            e.Handled = true;
        }
    }
}
