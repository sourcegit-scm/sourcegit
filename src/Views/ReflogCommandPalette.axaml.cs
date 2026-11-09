using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ReflogCommandPalette : UserControl
    {
        public ReflogCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.ReflogCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.NavigateTo();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (EntryListBox.IsKeyboardFocusWithin)
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
                    if (vm.VisibleEntries.Count > 0)
                        EntryListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (EntryListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.ReflogCommandPalette vm)
            {
                vm.NavigateTo();
                e.Handled = true;
            }
        }

        private async void OnResetToEntry(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReflogCommandPalette vm && sender is Button { DataContext: Models.ReflogEntry entry })
            {
                e.Handled = true;
                await vm.ResetToAsync(entry);
            }
        }
    }
}
