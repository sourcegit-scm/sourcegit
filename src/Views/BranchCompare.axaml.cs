using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class BranchCompare : ChromelessWindow
    {
        public BranchCompare()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.BranchCompare { SelectedChanges: { Count: 1 } selected } vm &&
                sender is ChangeCollectionView view)
            {
                var repo = vm.RepositoryPath;
                var change = selected[0];
                var menu = new ContextMenu();

                var openWithMerger = new MenuItem();
                openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                openWithMerger.Click += (_, ev) =>
                {
                    new Commands.DiffTool(repo, new Models.DiffOption(vm.Base.Head, vm.To.Head, change)).Open();
                    ev.Handled = true;
                };
                menu.Items.Add(openWithMerger);

                if (change.Index != Models.ChangeState.Deleted)
                {
                    var full = Path.GetFullPath(Path.Combine(repo, change.Path));
                    var explore = new MenuItem();
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = App.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = File.Exists(full);
                    explore.Click += (_, ev) =>
                    {
                        Native.OS.OpenInFileManager(full, true);
                        ev.Handled = true;
                    };
                    menu.Items.Add(explore);
                }

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copyPath.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(change.Path);
                    ev.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(copyPath);

                var copyFullPath = new MenuItem();
                copyFullPath.Header = App.Text("CopyFullPath");
                copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                copyFullPath.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(Native.OS.GetAbsPath(repo, change.Path));
                    ev.Handled = true;
                };
                menu.Items.Add(copyFullPath);
                menu.Open(view);
            }

            e.Handled = true;
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.BranchCompare vm && sender is TextBlock block)
                vm.NavigateTo(block.Text);

            e.Handled = true;
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.BranchCompare vm)
                return;

            if (sender is not ChangeCollectionView { SelectedChanges: { Count: 1 } selectedChanges })
                return;

            var change = selectedChanges[0];
            if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) && e.Key == Key.C)
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
