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
            get;
            private set;
        }

        public Models.Branch To
        {
            get;
            private set;
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
                        DiffContext = new DiffContext(_repo, new Models.DiffOption(Base.Head, To.Head, value[0]), _diffContext);
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

            Base = baseBranch;
            To = toBranch;

            Task.Run(() =>
            {
                var baseHead = new Commands.QuerySingleCommit(_repo, Base.Head).Result();
                var toHead = new Commands.QuerySingleCommit(_repo, To.Head).Result();
                _changes = new Commands.CompareRevisions(_repo, Base.Head, To.Head).Result();

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

                Dispatcher.UIThread.Invoke(() =>
                {
                    BaseHead = baseHead;
                    ToHead = toHead;
                    VisibleChanges = visible;
                });
            });
        }

        public void NavigateTo(string commitSHA)
        {
            var repo = App.FindOpenedRepository(_repo);
            repo?.NavigateToCommit(commitSHA);
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
                var opt = new Models.DiffOption(Base.Head, To.Head, change);

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
        private Models.Commit _baseHead = null;
        private Models.Commit _toHead = null;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<Models.Change> _selectedChanges = null;
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
