using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class RepositoryCommandPalette : UserControl
    {
        public RepositoryCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            FilterTextBox.Focus(NavigationMethod.Directional);
            CmdListBox.AddHandler(ListBox.KeyDownEvent, OnCmdListBoxKeyDown, handledEventsToo: true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.RepositoryCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.Exec();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (CmdListBox.IsKeyboardFocusWithin)
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
                    if (vm.VisibleCmds.Count > 0)
                        CmdListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (CmdListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnCmdListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.RepositoryCommandPalette vm)
            {
                vm.Exec();
                e.Handled = true;
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.RepositoryCommandPalette vm)
            {
                vm.Exec();
                e.Handled = true;
            }
        }
    }
}
