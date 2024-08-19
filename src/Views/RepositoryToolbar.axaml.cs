using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class RepositoryToolbar : UserControl
    {
        public RepositoryToolbar()
        {
            InitializeComponent();
        }

        private void OpenWithExternalTools(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForExternalTools();
                button.OpenContextMenu(menu);
                e.Handled = true;
            }
        }

        private async void OpenStatistics(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new Statistics() { DataContext = new ViewModels.Statistics(repo.FullPath) };
                await dialog.ShowDialog(owner);
                e.Handled = true;
            }
        }

        private async void OpenConfigure(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new RepositoryConfigure() { DataContext = new ViewModels.RepositoryConfigure(repo) };
                await dialog.ShowDialog(owner);
                e.Handled = true;
            }
        }

        private void Fetch(object _, RoutedEventArgs e)
        {
            var launcher = this.FindAncestorOfType<Launcher>();
            (DataContext as ViewModels.Repository)?.Fetch(launcher?.HasKeyModifier(KeyModifiers.Control) ?? false);
            e.Handled = true;
        }

        private void Pull(object _, RoutedEventArgs e)
        {
            var launcher = this.FindAncestorOfType<Launcher>();
            (DataContext as ViewModels.Repository)?.Pull(launcher?.HasKeyModifier(KeyModifiers.Control) ?? false);
            e.Handled = true;
        }

        private void Push(object _, RoutedEventArgs e)
        {
            var launcher = this.FindAncestorOfType<Launcher>();
            (DataContext as ViewModels.Repository)?.Push(launcher?.HasKeyModifier(KeyModifiers.Control) ?? false);
            e.Handled = true;
        }

        private void StashAll(object _, RoutedEventArgs e)
        {
            var launcher = this.FindAncestorOfType<Launcher>();
            (DataContext as ViewModels.Repository)?.StashAll(launcher?.HasKeyModifier(KeyModifiers.Control) ?? false);
            e.Handled = true;
        }

        private void OpenGitFlowMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForGitFlow();
                (sender as Control)?.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OpenGitLFSMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForGitLFS();
                (sender as Control)?.OpenContextMenu(menu);
            }

            e.Handled = true;
        }
    }
}

