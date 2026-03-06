using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class ExecuteCustomActionCommandPalette : UserControl
    {
        public ExecuteCustomActionCommandPalette()
        {
            InitializeComponent();
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.ExecuteCustomActionCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                await vm.ExecAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (ActionListBox.IsKeyboardFocusWithin)
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
                    if (vm.VisibleActions.Count > 0)
                        ActionListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (ActionListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private async void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.ExecuteCustomActionCommandPalette vm)
            {
                await vm.ExecAsync();
                e.Handled = true;
            }
        }
    }
}
