using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class AssumeUnchangedManager : ChromelessWindow
    {
        public AssumeUnchangedManager()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private async void OnRemoveButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.AssumeUnchangedManager vm && sender is Button button)
                await vm.RemoveAsync(button.DataContext as string);

            e.Handled = true;
        }
    }
}
