using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class LFSLocks : ChromelessWindow
    {
        public LFSLocks()
        {
            InitializeComponent();
        }

        private void OnUnlockButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LFSLocks vm && sender is Button button)
                vm.Unlock(button.DataContext as Models.LFSLock, false);

            e.Handled = true;
        }

        private void OnForceUnlockButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LFSLocks vm && sender is Button button)
                vm.Unlock(button.DataContext as Models.LFSLock, true);

            e.Handled = true;
        }
    }
}
