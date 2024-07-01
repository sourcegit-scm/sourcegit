﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class CompareTargetWorktree
    {
        public string SHA => string.Empty;
    }

    public class RevisionCompare : ObservableObject
    {
        public Models.Commit StartPoint
        {
            get;
            private set;
        }

        public object EndPoint
        {
            get;
            private set;
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
                        DiffContext = new DiffContext(_repo, new Models.DiffOption(StartPoint.SHA, _endPoint, value[0]), _diffContext);
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

        public RevisionCompare(string repo, Models.Commit startPoint, Models.Commit endPoint)
        {
            _repo = repo;
            StartPoint = startPoint;

            if (endPoint == null)
            {
                EndPoint = new CompareTargetWorktree();
                _endPoint = string.Empty;
            }
            else
            {
                EndPoint = endPoint;
                _endPoint = endPoint.SHA;
            }

            Task.Run(() =>
            {
                _changes = new Commands.CompareRevisions(_repo, startPoint.SHA, _endPoint).Result();

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

        public void Cleanup()
        {
            _repo = null;
            if (_changes != null)
                _changes.Clear();
            if (_visibleChanges != null)
                _visibleChanges.Clear();
            if (_selectedChanges != null)
                _selectedChanges.Clear();
            _searchFilter = null;
            _diffContext = null;
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
            diffWithMerger.Icon = App.CreateMenuIcon("Icons.Diff");
            diffWithMerger.Click += (_, ev) =>
            {
                var opt = new Models.DiffOption(StartPoint.SHA, _endPoint, change);
                var toolType = Preference.Instance.ExternalMergeToolType;
                var toolPath = Preference.Instance.ExternalMergeToolPath;

                Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, toolType, toolPath, opt));
                ev.Handled = true;
            };
            menu.Items.Add(diffWithMerger);

            if (change.Index != Models.ChangeState.Deleted)
            {
                var full = Path.GetFullPath(Path.Combine(_repo, change.Path));
                var explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Icon = App.CreateMenuIcon("Icons.Folder.Open");
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

        private string _repo = string.Empty;
        private string _endPoint = string.Empty;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<Models.Change> _selectedChanges = null;
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
