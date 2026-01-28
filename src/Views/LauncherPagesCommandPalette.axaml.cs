using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class LauncherPagesCommandPalette : UserControl
    {
        public LauncherPagesCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.LauncherPagesCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.OpenOrSwitchTo();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (RepoListBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisiblePages.Count > 0)
                    {
                        PageListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedPage = vm.VisiblePages[^1];
                    }
                    else
                    {
                        FilterTextBox.Focus(NavigationMethod.Directional);
                    }

                    e.Handled = true;
                    return;
                }

                if (PageListBox.IsKeyboardFocusWithin)
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
                    if (vm.VisiblePages.Count > 0)
                    {
                        PageListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedPage = vm.VisiblePages[0];
                    }
                    else if (vm.VisibleRepos.Count > 0)
                    {
                        RepoListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedRepo = vm.VisibleRepos[0];
                    }

                    e.Handled = true;
                    return;
                }

                if (PageListBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisibleRepos.Count > 0)
                    {
                        RepoListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedRepo = vm.VisibleRepos[0];
                    }

                    e.Handled = true;
                    return;
                }

                if (RepoListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPagesCommandPalette vm)
            {
                vm.OpenOrSwitchTo();
                e.Handled = true;
            }
        }
    }
}
