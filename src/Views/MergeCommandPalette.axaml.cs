using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class MergeCommandPalette : UserControl
    {
        public MergeCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.MergeCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.Launch();
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

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.MergeCommandPalette vm)
            {
                vm.Launch();
                e.Handled = true;
            }
        }
    }
}
