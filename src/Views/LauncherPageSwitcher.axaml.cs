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
            if (e.Key == Key.Down && PagesListBox.ItemCount > 0)
            {
                PagesListBox.Focus(NavigationMethod.Directional);

                if (PagesListBox.SelectedIndex < 0)
                    PagesListBox.SelectedIndex = 0;
                else if (PagesListBox.SelectedIndex < PagesListBox.ItemCount)
                    PagesListBox.SelectedIndex++;

                e.Handled = true;
            }
        }
    }
}
