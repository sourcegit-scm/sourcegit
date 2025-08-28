using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class WorkspaceSwitcher : UserControl
    {
        public WorkspaceSwitcher()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Enter && DataContext is ViewModels.WorkspaceSwitcher switcher)
            {
                switcher.Switch();
                e.Handled = true;
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.WorkspaceSwitcher switcher)
            {
                switcher.Switch();
                e.Handled = true;
            }
        }

        private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && WorkspaceListBox.ItemCount > 0)
            {
                WorkspaceListBox.Focus(NavigationMethod.Directional);

                if (WorkspaceListBox.SelectedIndex < 0)
                    WorkspaceListBox.SelectedIndex = 0;
                else if (WorkspaceListBox.SelectedIndex < WorkspaceListBox.ItemCount)
                    WorkspaceListBox.SelectedIndex++;

                e.Handled = true;
            }
        }
    }
}
