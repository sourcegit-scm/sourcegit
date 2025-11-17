using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class BranchCompareCommandPalette : UserControl
    {
        public BranchCompareCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.BranchCompareCommandPalette vm)
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
                    {
                        BranchListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedBranch = vm.Branches[0];
                    }

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
            if (DataContext is ViewModels.BranchCompareCommandPalette vm)
            {
                vm.Launch();
                e.Handled = true;
            }
        }
    }
}
