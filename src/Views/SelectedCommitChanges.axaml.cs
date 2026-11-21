using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class SelectedCommitChanges : UserControl
    {
        public SelectedCommitChanges()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is not ViewModels.CommitChanges { ChangeSelection: { Count: > 0 } selection } vm)
                return;

            var menu = new ContextMenu();
            if (selection is { Count: 1, HasFolder: false })
            {
                var externalDiff = new MenuItem
                {
                    Header = App.Text("OpenInExternalMergeTool"),
                    Icon = this.CreateMenuIcon("Icons.OpenWith"),
                    Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D",
                    IsEnabled = selection.Changes[0] is Models.CommitChange change && change.BeforeMode != "160000" && change.AfterMode != "160000",
                };
                externalDiff.Click += (_, ev) =>
                {
                    vm.OpenChangeWithExternalDiffTool(selection.Changes[0]);
                    ev.Handled = true;
                };
                menu.Items.Add(externalDiff);
            }

            var copyPath = new MenuItem
            {
                Header = App.Text("CopyPath"),
                Icon = this.CreateMenuIcon("Icons.Copy"),
                Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C",
            };
            copyPath.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(GetSelectedPaths(selection));
                ev.Handled = true;
            };
            menu.Items.Add(copyPath);
            menu.Open(sender as Control);
            e.Handled = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var commandKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (e.Key == Key.F && e.KeyModifiers == commandKey)
            {
                SelectedCommitChangesSearchBox.Focus();
                e.Handled = true;
            }
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.CommitChanges { ChangeSelection: { Count: > 0 } selection } vm)
                return;

            var commandKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (e.Key == Key.C && e.KeyModifiers == commandKey)
            {
                await this.CopyTextAsync(GetSelectedPaths(selection));
                e.Handled = true;
            }
            else if (e.Key == Key.D && e.KeyModifiers == (commandKey | KeyModifiers.Shift) &&
                     selection is { Count: 1, HasFolder: false })
            {
                vm.OpenChangeWithExternalDiffTool(selection.Changes[0]);
                e.Handled = true;
            }
        }

        private static string GetSelectedPaths(ViewModels.ChangeSelection selection)
        {
            return selection.IsSingleFolder
                ? selection.SingleFolderPath
                : string.Join(Environment.NewLine, selection.Changes.Select(change => change.Path));
        }
    }
}
