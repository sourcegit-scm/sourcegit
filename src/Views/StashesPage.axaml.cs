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
                    await this.CopyTextAsync(stash.Message);
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
                apply.Icon = this.CreateMenuIcon("Icons.CheckCircled");
                apply.Click += (_, ev) =>
                {
                    vm.Apply(stash);
                    ev.Handled = true;
                };

                var branch = new MenuItem();
                branch.Header = App.Text("StashCM.Branch");
                branch.Icon = this.CreateMenuIcon("Icons.Branch.Add");
                branch.Click += (_, ev) =>
                {
                    vm.CheckoutBranch(stash);
                    ev.Handled = true;
                };

                var drop = new MenuItem();
                drop.Header = App.Text("StashCM.Drop");
                drop.Icon = this.CreateMenuIcon("Icons.Clear");
                drop.Tag = "Back/Delete";
                drop.Click += (_, ev) =>
                {
                    vm.Drop(stash);
                    ev.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("StashCM.SaveAsPatch");
                patch.Icon = this.CreateMenuIcon("Icons.Save");
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
                        Models.Notification.Send(null, $"Failed to save as patch: {exception.Message}", true);
                    }

                    ev.Handled = true;
                };

                var copy = new MenuItem();
                copy.Header = App.Text("StashCM.CopyMessage");
                copy.Icon = this.CreateMenuIcon("Icons.Copy");
                copy.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copy.Click += async (_, ev) =>
                {
                    await this.CopyTextAsync(stash.Message);
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(apply);
                menu.Items.Add(branch);
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
            if (DataContext is ViewModels.StashesPage { ChangeSelection: { Count: > 0 } selection } vm)
            {
                var selectedSingleFolder = selection.IsSingleFolder;
                var fullPathOfFolder = selectedSingleFolder ? vm.GetAbsPath(selection.SingleFolderPath) : null;
                var relativePathOfFolder = selectedSingleFolder ? selection.SingleFolderPath : null;

                var menu = new ContextMenu();
                if (selection.Count == 1)
                {
                    var change = selection.Changes[0];
                    var changeFullPath = vm.GetAbsPath(change.Path);

                    if (!selection.HasFolder)
                    {
                        var openWithMerger = new MenuItem();
                        openWithMerger.Header = App.Text("OpenInExternalMergeTool");
                        openWithMerger.Icon = this.CreateMenuIcon("Icons.OpenWith");
                        openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
                        openWithMerger.Click += (_, ev) =>
                        {
                            vm.OpenChangeWithExternalDiffTool(change);
                            ev.Handled = true;
                        };
                        menu.Items.Add(openWithMerger);
                    }

                    if (!selection.HasFolder || selectedSingleFolder)
                    {
                        var explore = new MenuItem();
                        explore.Header = App.Text("RevealFile");
                        explore.Icon = this.CreateMenuIcon("Icons.Explore");
                        explore.IsEnabled = selectedSingleFolder ? Directory.Exists(fullPathOfFolder) : File.Exists(changeFullPath);
                        explore.Click += (_, ev) =>
                        {
                            Native.OS.OpenInFileManager(selectedSingleFolder ? fullPathOfFolder : changeFullPath);
                            ev.Handled = true;
                        };
                        menu.Items.Add(explore);
                    }

                    if (menu.Items.Count > 0)
                        menu.Items.Add(new MenuItem { Header = "-" });
                }

                var applyChanges = new MenuItem();
                applyChanges.Header = App.Text("StashCM.ApplyFileChanges");
                applyChanges.Icon = this.CreateMenuIcon("Icons.Diff");
                applyChanges.Click += async (_, ev) =>
                {
                    await vm.ApplySelectedChanges(selection.Changes);
                    ev.Handled = true;
                };

                var checkoutFiles = new MenuItem();
                checkoutFiles.Header = App.Text("ChangeCM.CheckoutThisRevision");
                checkoutFiles.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                checkoutFiles.Click += async (_, ev) =>
                {
                    await vm.CheckoutFilesAsync(selection.Changes);
                    ev.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
                copyPath.Click += async (_, ev) =>
                {
                    if (selectedSingleFolder)
                    {
                        await this.CopyTextAsync(relativePathOfFolder);
                    }
                    else if (selection.Changes.Count == 1)
                    {
                        await this.CopyTextAsync(selection.Changes[0].Path);
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        foreach (var c in selection.Changes)
                            builder.AppendLine(c.Path);

                        await this.CopyTextAsync(builder.ToString());
                    }

                    ev.Handled = true;
                };

                var copyFullPath = new MenuItem();
                copyFullPath.Header = App.Text("CopyFullPath");
                copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
                copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
                copyFullPath.Click += async (_, ev) =>
                {
                    if (selectedSingleFolder)
                    {
                        await this.CopyTextAsync(fullPathOfFolder);
                    }
                    else if (selection.Changes.Count == 1)
                    {
                        await this.CopyTextAsync(vm.GetAbsPath(selection.Changes[0].Path));
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        foreach (var c in selection.Changes)
                            builder.AppendLine(vm.GetAbsPath(c.Path));

                        await this.CopyTextAsync(builder.ToString());
                    }

                    ev.Handled = true;
                };

                menu.Items.Add(applyChanges);
                menu.Items.Add(checkoutFiles);
                menu.Items.Add(new MenuItem { Header = "-" });
                menu.Items.Add(copyPath);
                menu.Items.Add(copyFullPath);
                menu.Open(sender as Control);
            }

            e.Handled = true;
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.StashesPage { ChangeSelection: { Count: > 0 } selection } vm)
                return;

            if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) && e.Key == Key.C)
            {
                var builder = new StringBuilder();
                var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (selection.IsSingleFolder)
                {
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selection.SingleFolderPath) : selection.SingleFolderPath);
                }
                else if (selection.Changes.Count == 1)
                {
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selection.Changes[0].Path) : selection.Changes[0].Path);
                }
                else
                {
                    foreach (var c in selection.Changes)
                        builder.AppendLine(copyAbsPath ? vm.GetAbsPath(c.Path) : c.Path);
                }

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            }
        }
    }
}
