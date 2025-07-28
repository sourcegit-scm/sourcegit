using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Conflict : UserControl
    {
        public Conflict()
        {
            InitializeComponent();
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is { DataContext: ViewModels.Repository repo } && sender is TextBlock text)
                repo.NavigateToCommit(text.Text);

            e.Handled = true;
        }

        private async void OnUseTheirs(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.UseTheirsAsync();

            e.Handled = true;
        }

        private async void OnUseMine(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.UseMineAsync();

            e.Handled = true;
        }

        private async void OnOpenExternalMergeTool(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.OpenExternalMergeToolAsync();

            e.Handled = true;
        }
    }
}
