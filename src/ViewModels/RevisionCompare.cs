﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RevisionCompare : ObservableObject, IDisposable
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public object StartPoint
        {
            get => _startPoint;
            private set => SetProperty(ref _startPoint, value);
        }

        public object EndPoint
        {
            get => _endPoint;
            private set => SetProperty(ref _endPoint, value);
        }

        public bool CanSaveAsPatch { get; }

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
                    if (value is { Count: 1 })
                    {
                        var option = new Models.DiffOption(GetSHA(_startPoint), GetSHA(_endPoint), value[0]);
                        DiffContext = new DiffContext(_repo, option, _diffContext);
                    }
                    else
                    {
                        DiffContext = null;
                    }
                }
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

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public RevisionCompare(string repo, Models.Commit startPoint, Models.Commit endPoint)
        {
            _repo = repo;
            _startPoint = (object)startPoint ?? new Models.Null();
            _endPoint = (object)endPoint ?? new Models.Null();
            CanSaveAsPatch = startPoint != null && endPoint != null;

            Task.Run(Refresh);
        }

        public void Dispose()
        {
            _repo = null;
            _startPoint = null;
            _endPoint = null;
            _changes?.Clear();
            _visibleChanges?.Clear();
            _selectedChanges?.Clear();
            _searchFilter = null;
            _diffContext = null;
        }

        public void NavigateTo(string commitSHA)
        {
            var launcher = App.GetLauncher();
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
            (StartPoint, EndPoint) = (_endPoint, _startPoint);
            VisibleChanges = [];
            SelectedChanges = [];
            IsLoading = true;
            Task.Run(Refresh);
        }

        public void SaveAsPatch(string saveTo)
        {
            Task.Run(async () =>
            {
                var succ = await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo, _changes, GetSHA(_startPoint), GetSHA(_endPoint), saveTo);
                if (succ)
                    App.SendNotification(_repo, App.Text("SaveAsPatchSuccess"));
            });
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public ContextMenu CreateChangeContextMenu()
        {
            if (_selectedChanges is not { Count: 1 })
                return null;

            var change = _selectedChanges[0];
            var menu = new ContextMenu();

            var openWithMerger = new MenuItem();
            openWithMerger.Header = App.Text("OpenInExternalMergeTool");
            openWithMerger.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWithMerger.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+D" : "Ctrl+Shift+D";
            openWithMerger.Click += (_, ev) =>
            {
                var opt = new Models.DiffOption(GetSHA(_startPoint), GetSHA(_endPoint), change);
                var toolType = Preferences.Instance.ExternalMergeToolType;
                var toolPath = Preferences.Instance.ExternalMergeToolPath;
                new Commands.DiffTool(_repo, toolType, toolPath, opt).Open();
                ev.Handled = true;
            };
            menu.Items.Add(openWithMerger);

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
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(change.Path);
                ev.Handled = true;
            };
            menu.Items.Add(copyPath);

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = App.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await App.CopyTextAsync(Native.OS.GetAbsPath(_repo, change.Path));
                e.Handled = true;
            };
            menu.Items.Add(copyFullPath);

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

        private void Refresh()
        {
            _changes = new Commands.CompareRevisions(_repo, GetSHA(_startPoint), GetSHA(_endPoint)).ReadAsync().Result;

            var visible = _changes;
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                visible = [];
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                VisibleChanges = visible;
                IsLoading = false;

                if (VisibleChanges.Count > 0)
                    SelectedChanges = [VisibleChanges[0]];
                else
                    SelectedChanges = [];
            });
        }

        private string GetSHA(object obj)
        {
            return obj is Models.Commit commit ? commit.SHA : string.Empty;
        }

        private string _repo;
        private bool _isLoading = true;
        private object _startPoint;
        private object _endPoint;
        private List<Models.Change> _changes;
        private List<Models.Change> _visibleChanges;
        private List<Models.Change> _selectedChanges;
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext;
    }
}
