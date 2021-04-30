using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     显示提交中的变更列表
    /// </summary>
    public partial class CommitChanges : UserControl {
        private string repo = null;
        private List<Models.Commit> range = null;
        private List<Models.Change> cachedChanges = new List<Models.Change>();
        private string filter = null;

        public class ChangeNode {
            public string Path { get; set; } = "";
            public Models.Change Change { get; set; } = null;
            public bool IsExpanded { get; set; } = false;
            public bool IsFolder => Change == null;
            public List<ChangeNode> Children { get; set; } = new List<ChangeNode>();
        }

        public CommitChanges() {
            InitializeComponent();
        }

        public void SetData(string repo, List<Models.Commit> range, List<Models.Change> changes) {
            this.repo = repo;
            this.range = range;
            this.cachedChanges = changes;

            UpdateVisible();
        }

        public void UpdateVisible() {
            Task.Run(() => {
                // 筛选出可见的列表
                List<Models.Change> visible;
                if (string.IsNullOrEmpty(filter)) {
                    visible = cachedChanges;
                } else {
                    visible = cachedChanges.Where(x => x.Path.ToUpper().Contains(filter)).ToList();
                }

                // 排序
                visible.Sort((l, r) => l.Path.CompareTo(r.Path));

                // 生成树节点
                var nodes = new List<ChangeNode>();
                var folders = new Dictionary<string, ChangeNode>();
                var expanded = visible.Count <= 50;

                foreach (var c in visible) {
                    var sepIdx = c.Path.IndexOf('/');
                    if (sepIdx == -1) {
                        nodes.Add(new ChangeNode() {
                            Path = c.Path,
                            Change = c,
                            IsExpanded = false
                        });
                    } else {
                        ChangeNode lastFolder = null;
                        var start = 0;

                        while (sepIdx != -1) {
                            var folder = c.Path.Substring(0, sepIdx);
                            if (folders.ContainsKey(folder)) {
                                lastFolder = folders[folder];
                            } else if (lastFolder == null) {
                                lastFolder = new ChangeNode() {
                                    Path = folder,
                                    Change = null,
                                    IsExpanded = expanded
                                };
                                nodes.Add(lastFolder);
                                folders.Add(folder, lastFolder);
                            } else {
                                var cur = new ChangeNode() {
                                    Path = folder,
                                    Change = null,
                                    IsExpanded = expanded
                                };
                                folders.Add(folder, cur);
                                lastFolder.Children.Add(cur);
                                lastFolder = cur;
                            }

                            start = sepIdx + 1;
                            sepIdx = c.Path.IndexOf('/', start);
                        }

                        lastFolder.Children.Add(new ChangeNode() {
                            Path = c.Path,
                            Change = c,
                            IsExpanded = false
                        });
                    }
                }

                folders.Clear();
                SortFileNodes(nodes);

                Dispatcher.Invoke(() => {
                    modeTree.ItemsSource = nodes;
                    modeList.ItemsSource = visible;
                    modeGrid.ItemsSource = visible;

                    UpdateMode();
                });
            });
        }

        private void SortFileNodes(List<ChangeNode> nodes) {
            nodes.Sort((l, r) => {
                if (l.IsFolder == r.IsFolder) {
                    return l.Path.CompareTo(r.Path);
                } else {
                    return l.IsFolder ? -1 : 1;
                }
            });

            foreach (var node in nodes) {
                if (node.Children.Count > 1) SortFileNodes(node.Children);
            }
        }

        private void UpdateMode() {
            var mode = modeSwitcher.Mode;

            if (modeTree != null) {
                if (mode == Models.Change.DisplayMode.Tree) {
                    modeTree.Visibility = Visibility.Visible;
                } else {
                    modeTree.Visibility = Visibility.Collapsed;
                }
            }

            if (modeList != null) {
                if (mode == Models.Change.DisplayMode.List) {
                    modeList.Visibility = Visibility.Visible;
                    modeList.Columns[1].Width = DataGridLength.SizeToCells;
                    modeList.Columns[1].Width = DataGridLength.Auto;
                } else {
                    modeList.Visibility = Visibility.Collapsed;
                }
            }

            if (modeGrid != null) {
                if (mode == Models.Change.DisplayMode.Grid) {
                    modeGrid.Visibility = Visibility.Visible;
                    modeGrid.Columns[1].Width = DataGridLength.SizeToCells;
                    modeGrid.Columns[1].Width = DataGridLength.Auto;
                    modeGrid.Columns[2].Width = DataGridLength.SizeToCells;
                    modeGrid.Columns[2].Width = DataGridLength.Auto;
                } else {
                    modeGrid.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OpenChangeDiff(Models.Change change) {
            var revisions = new string[] { "", "" };
            if (range.Count == 2) {
                revisions[0] = range[0].SHA;
                revisions[1] = range[1].SHA;
            } else {
                revisions[0] = $"{range[0].SHA}^";
                revisions[1] = range[0].SHA;
                if (range[0].Parents.Count == 0) revisions[0] = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
            }

            diffViewer.Diff(repo, new DiffViewer.Option() {
                RevisionRange = revisions,
                Path = change.Path,
                OrgPath = change.OriginalPath
            });
        }

        private void OpenChangeContextMenu(Models.Change change) {
            var menu = new ContextMenu();
            var path = change.Path;

            if (change.Index != Models.Change.Status.Deleted) {
                var history = new MenuItem();
                history.Header = App.Text("FileHistory");
                history.Click += (o, ev) => {
                    var viewer = new Views.Histories(repo, path);
                    viewer.Show();
                    ev.Handled = true;
                };

                var blame = new MenuItem();
                blame.Header = App.Text("Blame");
                blame.Visibility = range.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
                blame.Click += (obj, ev) => {
                    var viewer = new Blame(repo, path, range[0].SHA);
                    viewer.Show();
                    ev.Handled = true;
                };

                var explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, ev) => {
                    var full = Path.GetFullPath(repo + "\\" + path);
                    Process.Start("explorer", $"/select,{full}");
                    ev.Handled = true;
                };

                menu.Items.Add(history);
                menu.Items.Add(blame);
                menu.Items.Add(explore);
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Click += (obj, ev) => {
                Clipboard.SetText(path);
            };

            menu.Items.Add(copyPath);
            menu.IsOpen = true;
        }

        private void OnDisplayModeChanged(object sender, RoutedEventArgs e) {
            UpdateMode();
        }

        private void SearchFilterChanged(object sender, TextChangedEventArgs e) {
            var edit = sender as Controls.TextEdit;
            filter = edit.Text.ToUpper();
            UpdateVisible();
        }

        private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        private void OnTreeSelectionChanged(object sender, RoutedEventArgs e) {
            if (modeSwitcher.Mode != Models.Change.DisplayMode.Tree) return;

            diffViewer.Reset();
            if (modeTree.Selected.Count == 0) return;

            var change = (modeTree.Selected[0] as ChangeNode).Change;
            if (change == null) return;

            OpenChangeDiff(change);
        }

        private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (modeSwitcher.Mode != Models.Change.DisplayMode.List) return;

            diffViewer.Reset();

            var change = (sender as DataGrid).SelectedItem as Models.Change;
            if (change == null) return;

            OpenChangeDiff(change);
        }

        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (modeSwitcher.Mode != Models.Change.DisplayMode.Grid) return;

            diffViewer.Reset();

            var change = (sender as DataGrid).SelectedItem as Models.Change;
            if (change == null) return;

            OpenChangeDiff(change);
        }

        private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var item = sender as Controls.TreeItem;
            if (item == null) return;

            var node = item.DataContext as ChangeNode;
            if (node == null || node.IsFolder) return;

            OpenChangeContextMenu(node.Change);
            e.Handled = true;
        }

        private void OnDataGridContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var row = sender as DataGridRow;
            if (row == null) return;

            var change = row.Item as Models.Change; 
            if (change == null) return;

            OpenChangeContextMenu(change);
            e.Handled = true;
        }

        private void OnListSizeChanged(object sender, SizeChangedEventArgs e) {
            if (modeSwitcher.Mode != Models.Change.DisplayMode.List) return;

            int last = modeList.Columns.Count - 1;
            double offset = 0;
            for (int i = 0; i < last; i++) offset += modeList.Columns[i].ActualWidth;
            modeList.Columns[last].MinWidth = Math.Max(layerChanges.ActualWidth - offset, 10);
            modeList.UpdateLayout();
        }

        private void OnGridSizeChanged(object sender, SizeChangedEventArgs e) {
            if (modeSwitcher.Mode != Models.Change.DisplayMode.Grid) return;

            int last = modeGrid.Columns.Count - 1;
            double offset = 0;
            for (int i = 0; i < last; i++) offset += modeGrid.Columns[i].ActualWidth;
            modeGrid.Columns[last].MinWidth = Math.Max(layerChanges.ActualWidth - offset, 10);
            modeGrid.UpdateLayout();
        }
    }
}
