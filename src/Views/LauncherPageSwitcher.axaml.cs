using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class LauncherPageSwitcher : UserControl
    {
        public LauncherPageSwitcher()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Enter && DataContext is ViewModels.LauncherPageSwitcher switcher)
            {
                switcher.Switch();
                e.Handled = true;
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPageSwitcher switcher)
            {
                switcher.Switch();
                e.Handled = true;
            }
        }

        private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (PagesListBox.ItemCount == 0)
                return;

            if (e.Key == Key.Down)
            {
                PagesListBox.Focus(NavigationMethod.Directional);

                if (PagesListBox.SelectedIndex < PagesListBox.ItemCount - 1)
                    PagesListBox.SelectedIndex++;
                else
                    PagesListBox.SelectedIndex = 0;

                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                PagesListBox.Focus(NavigationMethod.Directional);

                if (PagesListBox.SelectedIndex > 0)
                    PagesListBox.SelectedIndex--;
                else
                    PagesListBox.SelectedIndex = PagesListBox.ItemCount - 1;

                e.Handled = true;
            }
        }
    }
}
