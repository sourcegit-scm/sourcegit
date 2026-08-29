using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class DevSpaceFiles : UserControl
    {
        public DevSpaceFiles()
        {
            InitializeComponent();
        }

        private async void OnRefresh(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.DevSpaceFiles files)
                await files.RefreshAsync();

            e.Handled = true;
        }

        private void OnToggleExpanded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.DevSpaceFiles files &&
                sender is Button { DataContext: ViewModels.DevSpaceFileNode node })
            {
                files.ToggleExpanded(node);
            }

            e.Handled = true;
        }
    }
}
