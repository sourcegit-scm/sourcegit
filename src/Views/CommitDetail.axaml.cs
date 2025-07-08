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

        private async void OnCommitListKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Grid { DataContext: Models.Change change })
            {
                if (e.Key == Key.C && e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
                {
                    var path = change.Path;
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        path = detail.GetAbsPath(path);

                    await App.CopyTextAsync(path);
                    e.Handled = true;
                }
            }
        }
    }
}
