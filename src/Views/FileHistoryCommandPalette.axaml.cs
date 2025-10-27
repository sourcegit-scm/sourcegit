using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class FileHistoryCommandPalette : UserControl
    {
        public FileHistoryCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.FileHistoryCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.Launch();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (FileListBox.IsKeyboardFocusWithin)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Tab)
            {
                if (FilterTextBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisibleFiles.Count > 0)
                    {
                        FileListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedFile = vm.VisibleFiles[0];
                    }

                    e.Handled = true;
                    return;
                }

                if (FileListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.FileHistoryCommandPalette vm)
            {
                vm.Launch();
                e.Handled = true;
            }
        }
    }
}
