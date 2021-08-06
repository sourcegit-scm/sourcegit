using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     新标签页
    /// </summary>
    public partial class Welcome : UserControl, Controls.IPopupContainer {
        /// <summary>
        ///     树节点数据
        /// </summary>
        public class Node {
            public string Id { get; set; }
            public string ParentId { get; set; }
            public string Name { get; set; }
            public bool IsGroup { get; set; }
            public bool IsEditing { get; set; }
            public bool IsExpanded { get; set; }
            public int Bookmark { get; set; }
            public List<Node> Children { get; set; }
        }

        public Welcome() {
            InitializeComponent();
            UpdateTree();
        }

        #region POPUP_CONTAINER
        public void Show(Controls.PopupWidget widget) {
            popup.Show(widget);
        }

        public void ShowAndStart(Controls.PopupWidget widget) {
            popup.ShowAndStart(widget);
        }

        public void UpdateProgress(string message) {
            popup.UpdateProgress(message);
        }
        #endregion

        #region FUNC_EVENTS
        private void OnOpenClicked(object sender, RoutedEventArgs e) {
            var dialog = new Controls.FolderDialog();
            if (dialog.ShowDialog() == true) CheckAndOpen(dialog.SelectedPath);
        }

        private void OnCloneClicked(object sender, RoutedEventArgs e) {
            if (MakeSureReady()) new Popups.Clone().Show();
        }

        private void OnTreeNodeStatusChange(object sender, RoutedEventArgs e) {
            var node = (sender as Controls.TreeItem).DataContext as Node;
            if (node != null) {
                var group = Models.Preference.Instance.FindGroup(node.Id);
                group.IsExpanded = node.IsExpanded;
                e.Handled = true;
            }
        }

        private void OnTreeNodeDoubleClick(object sender, MouseButtonEventArgs e) {
            var node = (sender as Controls.TreeItem).DataContext as Node;
            if (node != null && !node.IsGroup) {
                CheckAndOpen(node.Id);
                e.Handled = true;
            }
        }

        private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var item = tree.FindItem(e.OriginalSource as DependencyObject);
            if (item == null) {
                var addFolder = new MenuItem();
                addFolder.Header = App.Text("Welcome.NewFolder");
                addFolder.Click += (o, ev) => {
                    var group = Models.Preference.Instance.AddGroup("New Group", "");
                    UpdateTree(group.Id);
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(addFolder);
                menu.IsOpen = true;
                e.Handled = true;
            } else {
                var node = item.DataContext as Node;
                if (node == null) return;

                var menu = new ContextMenu();
                if (!node.IsGroup) {
                    var open = new MenuItem();
                    open.Header = App.Text("RepoCM.Open");
                    open.Click += (o, ev) => {
                        CheckAndOpen(node.Id);
                        ev.Handled = true;
                    };

                    var explore = new MenuItem();
                    explore.Header = App.Text("RepoCM.Explore");
                    explore.Click += (o, ev) => {
                        Process.Start("explorer", node.Id);
                        ev.Handled = true;
                    };

                    var iconBookmark = FindResource("Icon.Bookmark") as Geometry;
                    var bookmark = new MenuItem();
                    bookmark.Header = App.Text("RepoCM.Bookmark");
                    for (int i = 0; i < Controls.Bookmark.COLORS.Length; i++) {
                        var icon = new System.Windows.Shapes.Path();
                        icon.Data = iconBookmark;
                        icon.Fill = Controls.Bookmark.COLORS[i];
                        icon.Width = 8;

                        var mark = new MenuItem();
                        mark.Icon = icon;
                        mark.Header = $"{i}";

                        var refIdx = i;
                        mark.Click += (o, ev) => {
                            var repo = Models.Preference.Instance.FindRepository(node.Id);
                            if (repo != null) {
                                repo.Bookmark = refIdx;
                                UpdateTree();
                            }
                            ev.Handled = true;
                        };

                        bookmark.Items.Add(mark);
                    }

                    menu.Items.Add(open);
                    menu.Items.Add(explore);
                    menu.Items.Add(bookmark);
                } else {
                    var addSubFolder = new MenuItem();
                    addSubFolder.Header = App.Text("Welcome.NewSubFolder");
                    addSubFolder.Click += (o, ev) => {
                        var parent = Models.Preference.Instance.FindGroup(node.Id);
                        if (parent != null) parent.IsExpanded = true;

                        var group = Models.Preference.Instance.AddGroup("New Group", node.Id);
                        UpdateTree(group.Id);
                        ev.Handled = true;
                    };

                    menu.Items.Add(addSubFolder);
                }

                var rename = new MenuItem();
                rename.Header = App.Text("Welcome.Rename");
                rename.Click += (o, ev) => {
                    UpdateTree(node.Id);
                    ev.Handled = true;
                };

                var delete = new MenuItem();
                delete.Header = App.Text("Welcome.Delete");
                delete.Click += (o, ev) => {
                    DeleteNode(node);
                    ev.Handled = true;
                };

                menu.Items.Add(rename);
                menu.Items.Add(delete);
                menu.IsOpen = true;
                e.Handled = true;
            }
        }
        #endregion

        #region DRAP_DROP_EVENTS
        private void OnPageDragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(typeof(Node))) {
                dropArea.Visibility = Visibility.Visible;
            }
        }

        private void OnPageDragLeave(object sender, DragEventArgs e) {
            dropArea.Visibility = Visibility.Hidden;
        }

        private void OnPageDrop(object sender, DragEventArgs e) {
            dropArea.Visibility = Visibility.Hidden;
        }

        private void OnTreeMouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var item = tree.FindItem(e.OriginalSource as DependencyObject);
            if (item == null) return;

            var adorner = new Controls.DragDropAdorner(item);
            DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
            adorner.Remove();
        }

        private void OnTreeDragOver(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(typeof(Node))) return;

            var item = tree.FindItem(e.OriginalSource as DependencyObject);
            if (item == null) return;

            var node = item.DataContext as Node;
            if (node.IsGroup && !item.IsExpanded) item.IsExpanded = true;
            e.Handled = true;
        }

        private void OnTreeDrop(object sender, DragEventArgs e) {
            bool rebuild = false;
            dropArea.Visibility = Visibility.Hidden;

            var parent = "";
            var to = tree.FindItem(e.OriginalSource as DependencyObject);
            if (to != null) {
                var dst = to.DataContext as Node;
                parent = dst.IsGroup ? dst.Id : dst.ParentId;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                if (!MakeSureReady()) return;

                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                foreach (var path in paths) {
                    var dir = new Commands.QueryGitDir(path).Result();
                    if (dir != null) {
                        var root = new Commands.GetRepositoryRootPath(path).Result();
                        Models.Preference.Instance.AddRepository(root, dir, parent);
                        CheckAndOpen(path);
                        rebuild = true;
                    }
                }
            } else if (e.Data.GetDataPresent(typeof(Node))) {
                var src = e.Data.GetData(typeof(Node)) as Node;
                if (src.IsGroup) {
                    if (!Models.Preference.Instance.IsSubGroup(src.Id, parent)) {
                        Models.Preference.Instance.FindGroup(src.Id).Parent = parent;
                        rebuild = true;
                    }
                } else {
                    Models.Preference.Instance.FindRepository(src.Id).GroupId = parent;
                    rebuild = true;
                }
            }

            if (rebuild) UpdateTree();
            e.Handled = true;
        }
        #endregion

        #region DATA
        private void UpdateTree(string editingNodeId = null) {
            var groupNodes = new Dictionary<string, Node>();
            var nodes = new List<Node>();

            foreach (var group in Models.Preference.Instance.Groups) {
                Node node = new Node() {
                    Id = group.Id,
                    ParentId = group.Parent,
                    Name = group.Name,
                    IsGroup = true,
                    IsEditing = group.Id == editingNodeId,
                    IsExpanded = group.IsExpanded,
                    Bookmark = 0,
                    Children = new List<Node>(),
                };

                groupNodes.Add(node.Id, node);
            }

            nodes.Clear();

            foreach (var kv in groupNodes) {
                if (groupNodes.ContainsKey(kv.Value.ParentId)) {
                    groupNodes[kv.Value.ParentId].Children.Add(kv.Value);
                } else {
                    nodes.Add(kv.Value);
                }
            }

            foreach (var repo in Models.Preference.Instance.Repositories) {
                Node node = new Node() {
                    Id = repo.Path,
                    ParentId = repo.GroupId,
                    Name = repo.Name,
                    IsGroup = false,
                    IsEditing = repo.Path == editingNodeId,
                    IsExpanded = false,
                    Bookmark = repo.Bookmark,
                    Children = new List<Node>(),
                };

                if (groupNodes.ContainsKey(repo.GroupId)) {
                    groupNodes[repo.GroupId].Children.Add(node);
                } else {
                    nodes.Add(node);
                }
            }

            tree.ItemsSource = nodes;
        }

        private void DeleteNode(Node node) {
            if (node.IsGroup) {
                Models.Preference.Instance.RemoveGroup(node.Id);
            } else {
                Models.Preference.Instance.RemoveRepository(node.Id);
            }

            UpdateTree();
        }

        private bool MakeSureReady() {
            if (!Models.Preference.Instance.IsReady) {
                Models.Exception.Raise(App.Text("NotConfigured"));
                return false;
            }
            return true;
        }

        private void CheckAndOpen(string path) {
            if (!MakeSureReady()) return;

            if (!Directory.Exists(path)) {
                Models.Exception.Raise(App.Text("PathNotFound", path));
                return;
            }

            var root = new Commands.GetRepositoryRootPath(path).Result();
            if (root == null) {
                new Popups.Init(path).Show();
                return;
            }

            var gitDir = new Commands.QueryGitDir(root).Result();
            var repo = Models.Preference.Instance.AddRepository(root, gitDir, "");
            Models.Watcher.Open(repo);
        }
        #endregion

        #region RENAME_NODES
        private void RenameStart(object sender, RoutedEventArgs e) {
            var edit = sender as Controls.TextEdit;
            if (edit == null || !edit.IsVisible) return;

            edit.SelectAll();
            edit.Focus();
        }

        private void RenameKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                UpdateTree();
                e.Handled = true;
            } else if (e.Key == Key.Enter) {
                RenameEnd(sender, e);
                e.Handled = true;
            }
        }

        private void RenameEnd(object sender, RoutedEventArgs e) {
            var edit = sender as Controls.TextEdit;
            if (edit == null) return;

            if (string.IsNullOrWhiteSpace(edit.Text)) {
                UpdateTree();
                e.Handled = false;
                return;
            }

            var node = edit.DataContext as Node;
            if (node != null) {
                if (node.IsGroup) {
                    Models.Preference.Instance.RenameGroup(node.Id, edit.Text);
                } else {
                    Models.Preference.Instance.RenameRepository(node.Id, edit.Text);
                }

                UpdateTree();
                e.Handled = false;
            }
        }
        #endregion
    }
}
