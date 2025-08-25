using System;

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

            if (sender is not ChangeCollectionView view)
                return;

            var detailView = this.FindAncestorOfType<CommitDetail>();
            if (detailView == null)
                return;

            var changes = view.SelectedChanges ?? [];
            var container = view.FindDescendantOfType<ChangeCollectionContainer>();
            if (container is { SelectedItems.Count: 1, SelectedItem: ViewModels.ChangeTreeNode { IsFolder: true } node })
            {
                var menu = detailView.CreateChangeContextMenuByFolder(node, changes);
                menu.Open(view);
                return;
            }

            if (changes.Count == 1)
            {
                var menu = detailView.CreateChangeContextMenu(changes[0]);
                menu.Open(view);
            }
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.CommitDetail vm)
                return;

            if (sender is not ChangeCollectionView { SelectedChanges: { Count: 1 } selectedChanges })
                return;

            var change = selectedChanges[0];
            if (e.Key == Key.C &&
                e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    await App.CopyTextAsync(vm.GetAbsPath(change.Path));
                else
                    await App.CopyTextAsync(change.Path);

                e.Handled = true;
            }
        }
    }
}
