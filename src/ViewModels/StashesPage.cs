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
    public class StashesPage : ObservableObject
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
                    }
                    else
                    {
                        Task.Run(() =>
                        {
                            var changes = null as List<Models.Change>;

                            if (Native.OS.GitVersion >= Models.GitVersions.STASH_SHOW_WITH_UNTRACKED)
                            {
                                changes = new Commands.QueryStashChanges(_repo.FullPath, value.Name).Result();
                            }
                            else
                            {
                                changes = new Commands.CompareRevisions(_repo.FullPath, $"{value.SHA}^", value.SHA).Result();
                                if (value.Parents.Count == 3)
                                {
                                    var untracked = new Commands.CompareRevisions(_repo.FullPath, "4b825dc642cb6eb9a060e54bf8d69288fbee4904", value.Parents[2]).Result();
                                    var needSort = changes.Count > 0;

                                    foreach (var c in untracked)
                                        changes.Add(c);

                                    if (needSort)
                                        changes.Sort((l, r) => string.Compare(l.Path, r.Path, StringComparison.Ordinal));
                                }
                            }

                            Dispatcher.UIThread.Invoke(() => Changes = changes);
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
                    else if (value.Index == Models.ChangeState.Added && _selectedStash.Parents.Count == 3)
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption("4b825dc642cb6eb9a060e54bf8d69288fbee4904", _selectedStash.Parents[2], value), _diffContext);
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

        public void Cleanup()
        {
            _repo = null;
            if (_stashes != null)
                _stashes.Clear();
            _selectedStash = null;
            if (_changes != null)
                _changes.Clear();
            _selectedChange = null;
            _diffContext = null;
        }

        public ContextMenu MakeContextMenu(Models.Stash stash)
        {
            if (stash == null)
                return null;

            var apply = new MenuItem();
            apply.Header = App.Text("StashCM.Apply");
            apply.Click += (_, ev) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new ApplyStash(_repo, stash));

                ev.Handled = true;
            };

            var drop = new MenuItem();
            drop.Header = App.Text("StashCM.Drop");
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
                        if (c.Index == Models.ChangeState.Added && _selectedStash.Parents.Count == 3)
                            opts.Add(new Models.DiffOption("4b825dc642cb6eb9a060e54bf8d69288fbee4904", _selectedStash.Parents[2], c));
                        else
                            opts.Add(new Models.DiffOption(_selectedStash.Parents[0], _selectedStash.SHA, c));
                    }

                    var succ = await Task.Run(() => Commands.SaveChangesAsPatch.ProcessStashChanges(_repo.FullPath, opts, storageFile.Path.LocalPath));
                    if (succ)
                        App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                }

                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(apply);
            menu.Items.Add(drop);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(patch);
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

                Task.Run(() => Commands.MergeTool.OpenForDiff(_repo.FullPath, toolType, toolPath, opt));
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
            resetToThisRevision.Click += (_, ev) =>
            {
                var log = _repo.CreateLog($"Reset File to '{_selectedStash.SHA}'");
                new Commands.Checkout(_repo.FullPath).Use(log).FileWithRevision(change.Path, $"{_selectedStash.SHA}");
                log.Complete();
                ev.Handled = true;
            };

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyPath.Click += (_, ev) =>
            {
                App.CopyText(change.Path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Click += (_, e) =>
            {
                App.CopyText(Native.OS.GetAbsPath(_repo.FullPath, change.Path));
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

        public void Clear()
        {
            if (_repo.CanCreatePopup())
                _repo.ShowPopup(new ClearStashes(_repo));
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
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
        private List<Models.Stash> _stashes = new List<Models.Stash>();
        private List<Models.Stash> _visibleStashes = new List<Models.Stash>();
        private string _searchFilter = string.Empty;
        private Models.Stash _selectedStash = null;
        private List<Models.Change> _changes = null;
        private Models.Change _selectedChange = null;
        private DiffContext _diffContext = null;
    }
}
