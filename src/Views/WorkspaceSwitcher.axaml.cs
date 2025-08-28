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
            if (WorkspaceListBox.ItemCount == 0)
                return;

            if (e.Key == Key.Down)
            {
                WorkspaceListBox.Focus(NavigationMethod.Directional);

                if (WorkspaceListBox.SelectedIndex < WorkspaceListBox.ItemCount - 1)
                    WorkspaceListBox.SelectedIndex++;
                else
                    WorkspaceListBox.SelectedIndex = 0;

                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                WorkspaceListBox.Focus(NavigationMethod.Directional);

                if (WorkspaceListBox.SelectedIndex > 0)
                    WorkspaceListBox.SelectedIndex--;
                else
                    WorkspaceListBox.SelectedIndex = WorkspaceListBox.ItemCount - 1;

                e.Handled = true;
            }
        }
    }
}
