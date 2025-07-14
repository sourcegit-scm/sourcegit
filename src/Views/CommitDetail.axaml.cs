using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class CommitDetail : UserControl
    {
        public CommitDetail()
        {
            InitializeComponent();
        }

        private async void OnCommitListKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail &&
                sender is ListBox { SelectedItem: Models.Change change } &&
                e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                if (e.Key == Key.C)
                {
                    var path = change.Path;
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        path = detail.GetAbsPath(path);

                    await App.CopyTextAsync(path);
                    e.Handled = true;
                }
                else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    detail.OpenChangeInMergeTool(change);
                    e.Handled = true;
                }
            }
        }

        private void OnChangeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Grid { DataContext: Models.Change change })
            {
                detail.ActivePageIndex = 1;
                detail.SelectedChanges = new() { change };
            }

            e.Handled = true;
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Grid { DataContext: Models.Change change } grid)
            {
                var menu = detail.CreateChangeContextMenu(change);
                menu?.Open(grid);
            }

            e.Handled = true;
        }
    }
}
