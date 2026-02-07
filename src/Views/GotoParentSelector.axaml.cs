using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class GotoParentSelector : ChromelessWindow
    {
        public GotoParentSelector()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private void OnListLoaded(object sender, RoutedEventArgs e)
        {
            (sender as ListBox)?.Focus();
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e is not { Key: Key.Enter, KeyModifiers: KeyModifiers.None })
                return;

            if (DataContext is not ViewModels.GotoParentSelector vm)
                return;

            if (sender is not ListBox { SelectedItem: Models.Commit commit })
                return;

            vm.Sure(commit);
            Close();
            e.Handled = true;
        }

        private void OnListItemTapped(object sender, TappedEventArgs e)
        {
            if (sender is not Control { DataContext: Models.Commit commit })
                return;

            if (DataContext is ViewModels.GotoParentSelector vm)
                vm.Sure(commit);

            Close();
            e.Handled = true;
        }
    }
}

