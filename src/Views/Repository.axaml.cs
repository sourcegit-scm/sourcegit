using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Repository : UserControl
    {
        public Repository()
        {
            InitializeComponent();
        }

        private async void OnOpenConfigure(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await this.ShowDialogAsync(new ViewModels.RepositoryConfigure(repo));
                e.Handled = true;
            }
        }

        private async void OnSkipInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.SkipMergeAsync();

            e.Handled = true;
        }

        private void OnResolveInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                repo.SelectedViewIndex = 1;

            e.Handled = true;
        }

        private async void OnAbortInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.AbortMergeAsync();

            e.Handled = true;
        }

        private void OnRightPagePropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Control.IsVisibleProperty && sender is Control page)
            {
                var diffViewer = page.FindDescendantOfType<DiffView>();
                diffViewer?.ToggleHotkeyBindings(page.IsVisible);
            }
        }
    }
}
