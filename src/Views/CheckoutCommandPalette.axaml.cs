using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class CheckoutCommandPalette : UserControl
    {
        public CheckoutCommandPalette()
        {
            InitializeComponent();
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.CheckoutCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                await vm.ExecAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (BranchListBox.IsKeyboardFocusWithin)
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
                    if (vm.Branches.Count > 0)
                        BranchListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (BranchListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private async void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.CheckoutCommandPalette vm)
            {
                await vm.ExecAsync();
                e.Handled = true;
            }
        }
    }
}
