using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
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
                            var changes = new Commands.QueryStashChanges(_repo.FullPath, value.SHA).Result();
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
                    SelectedChange = null;
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
                    else
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption($"{_selectedStash.SHA}^", _selectedStash.SHA, value), _diffContext);
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
                Task.Run(() => new Commands.Stash(_repo.FullPath).Apply(stash.Name));
                ev.Handled = true;
            };

            var pop = new MenuItem();
            pop.Header = App.Text("StashCM.Pop");
            pop.Click += (_, ev) =>
            {
                Task.Run(() => new Commands.Stash(_repo.FullPath).Pop(stash.Name));
                ev.Handled = true;
            };

            var drop = new MenuItem();
            drop.Header = App.Text("StashCM.Drop");
            drop.Click += (_, ev) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new DropStash(_repo.FullPath, stash));

                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(apply);
            menu.Items.Add(pop);
            menu.Items.Add(drop);
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
                var toolType = Preference.Instance.ExternalMergeToolType;
                var toolPath = Preference.Instance.ExternalMergeToolPath;
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
                new Commands.Checkout(_repo.FullPath).FileWithRevision(change.Path, $"{_selectedStash.SHA}");
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

            var copyFileName = new MenuItem();
            copyFileName.Header = App.Text("CopyFileName");
            copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFileName.Click += (_, e) =>
            {
                App.CopyText(Path.GetFileName(change.Path));
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(diffWithMerger);
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(resetToThisRevision);
            menu.Items.Add(new MenuItem { Header = "-" });
            menu.Items.Add(copyPath);
            menu.Items.Add(copyFileName);

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
