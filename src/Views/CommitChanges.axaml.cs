using System;
using System.Text;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class CommitChanges : UserControl
    {
        public CommitChanges()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            e.Handled = true;

            if (sender is not ChangeCollectionView { SelectedChanges: { Count: > 0 } changes } view)
                return;

            var detailView = this.FindAncestorOfType<CommitDetail>();
            if (detailView == null)
                return;

            var container = view.FindDescendantOfType<ChangeCollectionContainer>();
            if (container is { SelectedItems.Count: 1, SelectedItem: ViewModels.ChangeTreeNode { IsFolder: true } node })
                detailView.CreateChangeContextMenuByFolder(node, changes)?.Open(view);
            else if (changes.Count > 1)
                detailView.CreateMultipleChangesContextMenu(changes)?.Open(view);
            else
                detailView.CreateChangeContextMenu(changes[0])?.Open(view);
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.CommitDetail vm)
                return;

            if (sender is not ChangeCollectionView { SelectedChanges: { Count: > 0 } selectedChanges } view)
                return;

            if (e.Key == Key.C &&
                e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                var builder = new StringBuilder();
                var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                var container = view.FindDescendantOfType<ChangeCollectionContainer>();
                if (container is { SelectedItems.Count: 1, SelectedItem: ViewModels.ChangeTreeNode { IsFolder: true } node })
                {
                    builder.Append(copyAbsPath ? vm.GetAbsPath(node.FullPath) : node.FullPath);
                }
                else if (selectedChanges.Count == 1)
                {
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selectedChanges[0].Path) : selectedChanges[0].Path);
                }
                else
                {
                    foreach (var c in selectedChanges)
                        builder.AppendLine(copyAbsPath ? vm.GetAbsPath(c.Path) : c.Path);
                }

                await App.CopyTextAsync(builder.ToString());
                e.Handled = true;
            }
        }
    }
}
