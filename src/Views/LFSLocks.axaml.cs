using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class LFSLocks : ChromelessWindow
    {
        public LFSLocks()
        {
            InitializeComponent();
        }

        private void BeginMoveWindow(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
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
