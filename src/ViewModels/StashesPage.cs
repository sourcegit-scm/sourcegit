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
                                _untracked = untracked;
                                Changes = changes;
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
                    SelectedChange = value is { Count: > 0 } ? value[0] : null;
            }
        }

        public Models.Change SelectedChange
        {
            get => _selectedChange;
            set
            {
                if (SetProperty(ref _selectedChange, value))
                {
                    if (value == null)
                        DiffContext = null;
                    else if (_untracked.Contains(value))
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(Models.Commit.EmptyTreeSHA1, _selectedStash.Parents[2], value), _diffContext);
                    else
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_selectedStash.Parents[0], _selectedStash.SHA, value), _diffContext);
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
            _untracked.Clear();

            _repo = null;
            _selectedStash = null;
            _selectedChange = null;
            _diffContext = null;
        }

        public ContextMenu MakeContextMenu(Models.Stash stash)
        {
            if (stash == null)
                return null;

            var apply = new MenuItem();
            apply.Header = App.Text("StashCM.Apply");
            apply.Icon = App.CreateMenuIcon("Icons.CheckCircled");
            apply.Click += (_, ev) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new ApplyStash(_repo, stash));

                ev.Handled = true;
            };

            var drop = new MenuItem();
            drop.Header = App.Text("StashCM.Drop");
            drop.Icon = App.CreateMenuIcon("Icons.Clear");
            drop.Click += (_, ev) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new DropStash(_repo, stash));

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

        public ContextMenu MakeContextMenuForChange(Models.Change change)
        {
            if (change == null)
                return null;

            var diffWithMerger = new MenuItem();
            diffWithMerger.Header = App.Text("DiffWithMerger");
            diffWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
            diffWithMerger.Click += (_, ev) =>
            {
                var toolType = Preferences.Instance.ExternalMergeToolType;
                var toolPath = Preferences.Instance.ExternalMergeToolPath;
                var opt = new Models.DiffOption($"{_selectedStash.SHA}^", _selectedStash.SHA, change);

                Task.Run(() => Commands.MergeTool.OpenForDiffAsync(_repo.FullPath, toolType, toolPath, opt));
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
            copyPath.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(change.Path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(Native.OS.GetAbsPath(_repo.FullPath, change.Path));
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(diffWithMerger);
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

        public void Drop(Models.Stash stash)
        {
            if (stash != null && _repo.CanCreatePopup())
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
        private Models.Change _selectedChange = null;
        private DiffContext _diffContext = null;
    }
}
