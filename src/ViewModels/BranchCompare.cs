using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BranchCompare : ObservableObject
    {
        public Models.Branch Base
        {
            get => _based;
            private set => SetProperty(ref _based, value);
        }

        public Models.Branch To
        {
            get => _to;
            private set => SetProperty(ref _to, value);
        }

        public Models.Commit BaseHead
        {
            get => _baseHead;
            private set => SetProperty(ref _baseHead, value);
        }

        public Models.Commit ToHead
        {
            get => _toHead;
            private set => SetProperty(ref _toHead, value);
        }

        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            private set => SetProperty(ref _visibleChanges, value);
        }

        public List<Models.Change> SelectedChanges
        {
            get => _selectedChanges;
            set
            {
                if (SetProperty(ref _selectedChanges, value))
                {
                    if (value != null && value.Count == 1)
                        DiffContext = new DiffContext(_repo, new Models.DiffOption(_based.Head, _to.Head, value[0]), _diffContext);
                    else
                        DiffContext = null;
                }
            }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                {
                    RefreshVisible();
                }
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public BranchCompare(string repo, Models.Branch baseBranch, Models.Branch toBranch)
        {
            _repo = repo;
            _based = baseBranch;
            _to = toBranch;

            Refresh();
        }

        public void NavigateTo(string commitSHA)
        {
            var launcher = App.GetLauncer();
            if (launcher == null)
                return;

            foreach (var page in launcher.Pages)
            {
                if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                {
                    repo.NavigateToCommit(commitSHA);
                    break;
                }
            }
        }

        public void Swap()
        {
            (Base, To) = (_to, _based);
            SelectedChanges = [];

            if (_baseHead != null)
                (BaseHead, ToHead) = (_toHead, _baseHead);

            Refresh();
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public ContextMenu CreateChangeContextMenu()
        {
            if (_selectedChanges == null || _selectedChanges.Count != 1)
                return null;

            var change = _selectedChanges[0];
            var menu = new ContextMenu();

            var diffWithMerger = new MenuItem();
            diffWithMerger.Header = App.Text("DiffWithMerger");
            diffWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
            diffWithMerger.Click += (_, ev) =>
            {
                var toolType = Preference.Instance.ExternalMergeToolType;
                var toolPath = Preference.Instance.ExternalMergeToolPath;
                var opt = new Models.DiffOption(_based.Head, _to.Head, change);

                Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, toolType, toolPath, opt));
                ev.Handled = true;
            };
            menu.Items.Add(diffWithMerger);

            if (change.Index != Models.ChangeState.Deleted)
            {
                var full = Path.GetFullPath(Path.Combine(_repo, change.Path));
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
            copyPath.Click += (_, ev) =>
            {
                App.CopyText(change.Path);
                ev.Handled = true;
            };
            menu.Items.Add(copyPath);

            var copyFileName = new MenuItem();
            copyFileName.Header = App.Text("CopyFileName");
            copyFileName.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFileName.Click += (_, e) =>
            {
                App.CopyText(Path.GetFileName(change.Path));
                e.Handled = true;
            };
            menu.Items.Add(copyFileName);

            return menu;
        }

        private void Refresh()
        {
            Task.Run(() =>
            {
                if (_baseHead == null)
                {
                    var baseHead = new Commands.QuerySingleCommit(_repo, _based.Head).Result();
                    var toHead = new Commands.QuerySingleCommit(_repo, _to.Head).Result();
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        BaseHead = baseHead;
                        ToHead = toHead;
                    });
                }

                _changes = new Commands.CompareRevisions(_repo, _based.Head, _to.Head).Result();

                var visible = _changes;
                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    visible = new List<Models.Change>();
                    foreach (var c in _changes)
                    {
                        if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(c);
                    }
                }

                Dispatcher.UIThread.Invoke(() => VisibleChanges = visible);
            });
        }

        private void RefreshVisible()
        {
            if (_changes == null)
                return;

            if (string.IsNullOrEmpty(_searchFilter))
            {
                VisibleChanges = _changes;
            }
            else
            {
                var visible = new List<Models.Change>();
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }

                VisibleChanges = visible;
            }
        }

        private string _repo;
        private Models.Branch _based = null;
        private Models.Branch _to = null;
        private Models.Commit _baseHead = null;
        private Models.Commit _toHead = null;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<Models.Change> _selectedChanges = null;
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
