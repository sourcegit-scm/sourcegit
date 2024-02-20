using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class RevisionCompare : ObservableObject {
        public Models.Commit StartPoint {
            get;
            private set;
        }

        public Models.Commit EndPoint {
            get;
            private set;
        }

        public List<Models.Change> VisibleChanges {
            get => _visibleChanges;
            private set => SetProperty(ref _visibleChanges, value);
        }

        public List<FileTreeNode> ChangeTree {
            get => _changeTree;
            private set => SetProperty(ref _changeTree, value);
        }

        public Models.Change SelectedChange {
            get => _selectedChange;
            set {
                if (SetProperty(ref _selectedChange, value)) {
                    if (value == null) {
                        SelectedNode = null;
                        DiffContext = null;
                    } else {
                        SelectedNode = FileTreeNode.SelectByPath(_changeTree, value.Path);
                        DiffContext = new DiffContext(_repo, new Models.DiffOption(StartPoint.SHA, EndPoint.SHA, value));
                    }
                }
            }
        }

        public FileTreeNode SelectedNode {
            get => _selectedNode;
            set {
                if (SetProperty(ref _selectedNode, value)) {
                    if (value == null) {
                        SelectedChange = null;
                    } else {
                        SelectedChange = value.Backend as Models.Change;
                    }
                }
            }
        }

        public string SearchFilter {
            get => _searchFilter;
            set {
                if (SetProperty(ref _searchFilter, value)) {
                    RefreshVisible();
                }
            }
        }

        public DiffContext DiffContext {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public RevisionCompare(string repo, Models.Commit startPoint, Models.Commit endPoint) {
            _repo = repo;
            StartPoint = startPoint;
            EndPoint = endPoint;

            Task.Run(() => {
                _changes = new Commands.CompareRevisions(_repo, startPoint.SHA, endPoint.SHA).Result();

                var visible = _changes;
                if (!string.IsNullOrWhiteSpace(_searchFilter)) {
                    visible = new List<Models.Change>();
                    foreach (var c in _changes) {
                        if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)) {
                            visible.Add(c);
                        }
                    }
                }

                var tree = FileTreeNode.Build(visible);
                Dispatcher.UIThread.Invoke(() => {
                    VisibleChanges = visible;
                    ChangeTree = tree;
                });
            });
        }

        public void Cleanup() {
            _repo = null;
            if (_changes != null) _changes.Clear();
            if (_visibleChanges != null) _visibleChanges.Clear();
            if (_changeTree != null) _changeTree.Clear();
            _selectedChange = null;
            _selectedNode = null;
            _searchFilter = null;
            _diffContext = null;
        }

        public void NavigateTo(string commitSHA) {
            var repo = Preference.FindRepository(_repo);
            if (repo != null) repo.NavigateToCommit(commitSHA);
        }

        public void ClearSearchFilter() {
            SearchFilter = string.Empty;
        }

        public ContextMenu CreateChangeContextMenu(Models.Change change) {
            var menu = new ContextMenu();

            if (change.Index != Models.ChangeState.Deleted) {
                var history = new MenuItem();
                history.Header = App.Text("FileHistory");
                history.Click += (_, ev) => {
                    var window = new Views.FileHistories() { DataContext = new FileHistories(_repo, change.Path) };
                    window.Show();
                    ev.Handled = true;
                };

                var full = Path.GetFullPath(Path.Combine(_repo, change.Path));
                var explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.IsEnabled = File.Exists(full);
                explore.Click += (_, ev) => {
                    Native.OS.OpenInFileManager(full, true);
                    ev.Handled = true;
                };

                menu.Items.Add(history);
                menu.Items.Add(explore);
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Click += (_, ev) => {
                App.CopyText(change.Path);
                ev.Handled = true;
            };

            menu.Items.Add(copyPath);
            return menu;
        }

        private void RefreshVisible() {
            if (_changes == null) return;

            if (string.IsNullOrEmpty(_searchFilter)) {
                VisibleChanges = _changes;
            } else {
                var visible = new List<Models.Change>();
                foreach (var c in _changes) {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)) {
                        visible.Add(c);
                    }
                }

                VisibleChanges = visible;
            }

            ChangeTree = FileTreeNode.Build(_visibleChanges);
        }

        private string _repo = string.Empty;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<FileTreeNode> _changeTree = null;
        private Models.Change _selectedChange = null;
        private FileTreeNode _selectedNode = null;
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
