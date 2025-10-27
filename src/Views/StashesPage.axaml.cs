using System;
using System.IO;
using System.Text;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class StashesPage : UserControl
    {
        public StashesPage()
        {
            InitializeComponent();
        }

        private void OnMainLayoutSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            var layout = ViewModels.Preferences.Instance.Layout;
            var width = grid.Bounds.Width;
            var maxLeft = width - 304;

            if (layout.StashesLeftWidth.Value - maxLeft > 1.0)
                layout.StashesLeftWidth = new GridLength(maxLeft, GridUnitType.Pixel);
        }

        private async void OnStashListKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage { SelectedStash: { } stash } vm)
            {
                if (e.Key is Key.Delete or Key.Back)
                {
                    vm.Drop(stash);
                    e.Handled = true;
                }
                else if (e.Key is Key.C && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
                {
                    await App.CopyTextAsync(stash.Message);
                    e.Handled = true;
                }
            }
        }

        private void OnStashContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm &&
                sender is Border { DataContext: Models.Stash stash } border)
            {
                var apply = new MenuItem();
                apply.Header = App.Text("StashCM.Apply");
                apply.Icon = App.CreateMenuIcon("Icons.CheckCircled");
                apply.Click += (_, ev) =>
                {
                    vm.Apply(stash);
                    ev.Handled = true;
                };

                var drop = new MenuItem();
                drop.Header = App.Text("StashCM.Drop");
                drop.Icon = App.CreateMenuIcon("Icons.Clear");
                drop.Tag = "Back/Delete";
                drop.Click += (_, ev) =>
                {
                    vm.Drop(stash);
                    ev.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("StashCM.SaveAsPatch");
                patch.Icon = App.CreateMenuIcon("Icons.Diff");
                patch.Click += async (_, ev) =>
                {
                    var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                    if (storageProvider == null)
                        return;

                    var options = new FilePickerSaveOptions();
                    options.Title = App.Text("StashCM.SaveAsPatch");
                    options.DefaultExtension = ".patch";
                    options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                    try
                    {
                        var storageFile = await storageProvider.SaveFilePickerAsync(options);
                        if (storageFile != null)
                            await vm.SaveStashAsPatchAsync(stash, storageFile.Path.LocalPath);
                    }
                    catch (Exception exception)
                    {
                        App.RaiseException(string.Empty, $"Failed to save as patch: {exception.Message}");
                    }

                    ev.Handled = true;
                };

                var copy = new MenuItem();
                copy.Header = App.Text("StashCM.CopyMessage");
                copy.Icon = App.CreateMenuIcon("Icons.Copy");
                copy.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copy.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(stash.Message);
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(apply);
                menu.Items.Add(drop);
                menu.Items.Add(new MenuItem { Header = "-" });
                menu.Items.Add(patch);
                menu.Items.Add(new MenuItem { Header = "-" });
                menu.Items.Add(copy);
                menu.Open(border);
            }

            e.Handled = true;
        }

        private void OnStashDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage vm &&
                sender is Border { DataContext: Models.Stash stash })
                vm.Apply(stash);

            e.Handled = true;
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.StashesPage { SelectedChanges: { Count: > 0 } selected } vm &&
                sender is ChangeCollectionView view)
            {
                if (selected.Count == 1)
                {
                    var change = selected[0];
                    var fullPath = vm.GetAbsPath(change.Path);

                    var openWithMerger = new MenuItem();
                    openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                    openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
                    openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                    openWithMerger.Click += (_, ev) =>
                    {
                        vm.OpenChangeWithExternalDiffTool(change);
                        ev.Handled = true;
                    };

                    var explore = new MenuItem();
                    explore.Header = App.Text("RevealFile");
                    explore.Icon = App.CreateMenuIcon("Icons.Explore");
                    explore.IsEnabled = File.Exists(fullPath);
                    explore.Click += (_, ev) =>
                    {
                        Native.OS.OpenInFileManager(fullPath, true);
                        ev.Handled = true;
                    };

                    var resetToThisRevision = new MenuItem();
                    resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
                    resetToThisRevision.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                    resetToThisRevision.Click += async (_, ev) =>
                    {
                        await vm.CheckoutSingleFileAsync(change);
                        ev.Handled = true;
                    };

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(change.Path);
                        ev.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(fullPath);
                        ev.Handled = true;
                    };

                    var menu = new ContextMenu();
                    menu.Items.Add(openWithMerger);
                    menu.Items.Add(explore);
                    menu.Items.Add(new MenuItem { Header = "-" });
                    menu.Items.Add(resetToThisRevision);
                    menu.Items.Add(new MenuItem { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                    menu.Open(view);
                }
                else
                {
                    var resetToThisRevision = new MenuItem();
                    resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
                    resetToThisRevision.Icon = App.CreateMenuIcon("Icons.File.Checkout");
                    resetToThisRevision.Click += async (_, ev) =>
                    {
                        await vm.CheckoutMultipleFileAsync(selected);
                        ev.Handled = true;
                    };

                    var copyPath = new MenuItem();
                    copyPath.Header = App.Text("CopyPath");
                    copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                    copyPath.Click += async (_, ev) =>
                    {
                        var builder = new StringBuilder();
                        foreach (var c in selected)
                            builder.AppendLine(c.Path);

                        await App.CopyTextAsync(builder.ToString());
                        ev.Handled = true;
                    };

                    var copyFullPath = new MenuItem();
                    copyFullPath.Header = App.Text("CopyFullPath");
                    copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
                    copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                    copyFullPath.Click += async (_, ev) =>
                    {
                        var builder = new StringBuilder();
                        foreach (var c in selected)
                            builder.AppendLine(vm.GetAbsPath(c.Path));

                        await App.CopyTextAsync(builder.ToString());
                        ev.Handled = true;
                    };

                    var menu = new ContextMenu();
                    menu.Items.Add(resetToThisRevision);
                    menu.Items.Add(new MenuItem { Header = "-" });
                    menu.Items.Add(copyPath);
                    menu.Items.Add(copyFullPath);
                    menu.Open(view);
                }
            }

            e.Handled = true;
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.StashesPage vm)
                return;

            if (sender is not ChangeCollectionView { SelectedChanges: { Count: > 0 } selectedChanges })
                return;

            if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) && e.Key == Key.C)
            {
                var builder = new StringBuilder();
                var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (selectedChanges.Count == 1)
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
