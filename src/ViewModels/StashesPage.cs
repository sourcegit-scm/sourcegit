using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class StashesPage : ObservableObject, IDisposable
    {
        public List<Models.Stash> Stashes
        {
            get => _stashes;
            set
            {
                if (SetProperty(ref _stashes, value))
                    RefreshVisible();
            }
        }

        public List<Models.Stash> VisibleStashes
        {
            get => _visibleStashes;
            private set
            {
                if (SetProperty(ref _visibleStashes, value))
                    SelectedStash = null;
            }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    RefreshVisible();
            }
        }

        public Models.Stash SelectedStash
        {
            get => _selectedStash;
            set
            {
                if (SetProperty(ref _selectedStash, value))
                {
                    if (value == null)
                    {
                        Changes = null;
                        _untracked.Clear();
                    }
                    else
                    {
                        Task.Run(async () =>
                        {
                            var changes = await new Commands.CompareRevisions(_repo.FullPath, $"{value.SHA}^", value.SHA)
                                .ReadAsync()
                                .ConfigureAwait(false);

                            var untracked = new List<Models.Change>();
                            if (value.Parents.Count == 3)
                            {
                                untracked = await new Commands.CompareRevisions(_repo.FullPath, Models.Commit.EmptyTreeSHA1, value.Parents[2])
                                    .ReadAsync()
                                    .ConfigureAwait(false);

                                var needSort = changes.Count > 0 && untracked.Count > 0;
                                changes.AddRange(untracked);
                                if (needSort)
                                    changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
                            }

                            Dispatcher.UIThread.Post(() =>
                            {
                                if (value.SHA.Equals(_selectedStash?.SHA ?? string.Empty, StringComparison.Ordinal))
                                {
                                    _untracked = untracked;
                                    Changes = changes;
                                }
                            });
                        });
                    }
                }
            }
        }

        public List<Models.Change> Changes
        {
            get => _changes;
            private set
            {
                if (SetProperty(ref _changes, value))
                    SelectedChanges = value is { Count: > 0 } ? [value[0]] : [];
            }
        }

        public List<Models.Change> SelectedChanges
        {
            get => _selectedChanges;
            set
            {
                if (SetProperty(ref _selectedChanges, value))
                {
                    if (value is not { Count: 1 })
                        DiffContext = null;
                    else if (_untracked.Contains(value[0]))
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(Models.Commit.EmptyTreeSHA1, _selectedStash.Parents[2], value[0]), _diffContext);
                    else
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_selectedStash.Parents[0], _selectedStash.SHA, value[0]), _diffContext);
                }
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public StashesPage(Repository repo)
        {
            _repo = repo;
        }

        public void Dispose()
        {
            _stashes?.Clear();
            _changes?.Clear();
            _selectedChanges?.Clear();
            _untracked.Clear();

            _repo = null;
            _selectedStash = null;
            _diffContext = null;
        }

        public ContextMenu MakeContextMenu(Models.Stash stash)
        {
            var apply = new MenuItem();
            apply.Header = App.Text("StashCM.Apply");
            apply.Icon = App.CreateMenuIcon("Icons.CheckCircled");
            apply.Click += (_, ev) =>
            {
                Apply(stash);
                ev.Handled = true;
            };

            var drop = new MenuItem();
            drop.Header = App.Text("StashCM.Drop");
            drop.Icon = App.CreateMenuIcon("Icons.Clear");
            drop.Tag = "Back/Delete";
            drop.Click += (_, ev) =>
            {
                Drop(stash);
                ev.Handled = true;
            };

            var patch = new MenuItem();
            patch.Header = App.Text("StashCM.SaveAsPatch");
            patch.Icon = App.CreateMenuIcon("Icons.Diff");
            patch.Click += async (_, e) =>
            {
                var storageProvider = App.GetStorageProvider();
                if (storageProvider == null)
                    return;

                var options = new FilePickerSaveOptions();
                options.Title = App.Text("StashCM.SaveAsPatch");
                options.DefaultExtension = ".patch";
                options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

                var storageFile = await storageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var opts = new List<Models.DiffOption>();
                    foreach (var c in _changes)
                    {
                        if (_untracked.Contains(c))
                            opts.Add(new Models.DiffOption(Models.Commit.EmptyTreeSHA1, _selectedStash.Parents[2], c));
                        else
                            opts.Add(new Models.DiffOption(_selectedStash.Parents[0], _selectedStash.SHA, c));
                    }

                    var succ = await Commands.SaveChangesAsPatch.ProcessStashChangesAsync(_repo.FullPath, opts, storageFile.Path.LocalPath);
                    if (succ)
                        App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                }

                e.Handled = true;
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
            return menu;
        }

        public ContextMenu MakeContextMenuForChange()
        {
            if (_selectedChanges is not { Count: 1 })
                return null;

            var change = _selectedChanges[0];
            var openWithMerger = new MenuItem();
            openWithMerger.Header = App.Text("OpenInExternalMergeTool");
            openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
            openWithMerger.Click += (_, ev) =>
            {
                var toolType = Preferences.Instance.ExternalMergeToolType;
                var toolPath = Preferences.Instance.ExternalMergeToolPath;
                var opt = new Models.DiffOption($"{_selectedStash.SHA}^", _selectedStash.SHA, change);
                new Commands.DiffTool(_repo.FullPath, toolType, toolPath, opt).Open();
                ev.Handled = true;
            };

            var fullPath = Path.Combine(_repo.FullPath, change.Path);
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
                var log = _repo.CreateLog($"Reset File to '{_selectedStash.SHA}'");

                if (_untracked.Contains(change))
                {
                    await Commands.SaveRevisionFile.RunAsync(_repo.FullPath, _selectedStash.Parents[2], change.Path, fullPath);
                }
                else if (change.Index == Models.ChangeState.Added)
                {
                    await Commands.SaveRevisionFile.RunAsync(_repo.FullPath, _selectedStash.SHA, change.Path, fullPath);
                }
                else
                {
                    await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .FileWithRevisionAsync(change.Path, $"{_selectedStash.SHA}");
                }

                log.Complete();
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
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(Native.OS.GetAbsPath(_repo.FullPath, change.Path));
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(openWithMerger);
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(resetToThisRevision);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);

            return menu;
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public void Apply(Models.Stash stash)
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new ApplyStash(_repo, stash));
        }

        public void Drop(Models.Stash stash)
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new DropStash(_repo, stash));
        }

        private void RefreshVisible()
        {
            if (string.IsNullOrEmpty(_searchFilter))
            {
                VisibleStashes = _stashes;
            }
            else
            {
                var visible = new List<Models.Stash>();
                foreach (var s in _stashes)
                {
                    if (s.Message.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(s);
                }

                VisibleStashes = visible;
            }
        }

        private Repository _repo = null;
        private List<Models.Stash> _stashes = [];
        private List<Models.Stash> _visibleStashes = [];
        private string _searchFilter = string.Empty;
        private Models.Stash _selectedStash = null;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _untracked = [];
        private List<Models.Change> _selectedChanges = [];
        private DiffContext _diffContext = null;
    }
}
