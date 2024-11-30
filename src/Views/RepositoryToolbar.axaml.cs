using System;

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
                menu?.Open(button);
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
            if (launcher is not null && DataContext is ViewModels.Repository repo)
            {
                var startDirectly = launcher.HasKeyModifier(KeyModifiers.Control);
                launcher.ClearKeyModifier();
                repo.Fetch(startDirectly);
                e.Handled = true;
            }
        }

        private void Pull(object _, RoutedEventArgs e)
        {
            var launcher = this.FindAncestorOfType<Launcher>();
            if (launcher is not null && DataContext is ViewModels.Repository repo)
            {
                var startDirectly = launcher.HasKeyModifier(KeyModifiers.Control);
                launcher.ClearKeyModifier();
                repo.Pull(startDirectly);
                e.Handled = true;
            }
        }

        private void Push(object _, RoutedEventArgs e)
        {
            var launcher = this.FindAncestorOfType<Launcher>();
            if (launcher is not null && DataContext is ViewModels.Repository repo)
            {
                var startDirectly = launcher.HasKeyModifier(KeyModifiers.Control);
                launcher.ClearKeyModifier();
                repo.Push(startDirectly);
                e.Handled = true;
            }
        }

        private void StashAll(object _, RoutedEventArgs e)
        {
            var launcher = this.FindAncestorOfType<Launcher>();
            if (launcher is not null && DataContext is ViewModels.Repository repo)
            {
                var startDirectly = launcher.HasKeyModifier(KeyModifiers.Control);
                launcher.ClearKeyModifier();
                repo.StashAll(startDirectly);
                e.Handled = true;
            }
        }

        private void OpenGitFlowMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = repo.CreateContextMenuForGitFlow();
                menu?.Open(control);
            }

            e.Handled = true;
        }

        private void OpenGitLFSMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = repo.CreateContextMenuForGitLFS();
                menu?.Open(control);
            }

            e.Handled = true;
        }

        private void OpenCustomActionMenu(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = repo.CreateContextMenuForCustomAction();
                menu?.Open(control);
            }

            e.Handled = true;
        }
    }
}

