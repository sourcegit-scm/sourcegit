using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     New page to open repository.
    /// </summary>
    public partial class NewPage : UserControl {

        /// <summary>
        ///     Used to build tree
        /// </summary>
        public class Node {
            public string Id { get; set; }
            public string ParentId { get; set; }
            public string Name { get; set; }
            public bool IsRepo { get; set; }
            public bool IsExpended { get; set; }
            public bool IsEditing { get; set; }
            public int Color { get; set; }
            public List<Node> Children { get; set; } = new List<Node>();
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public NewPage() {
            InitializeComponent();
            UpdateTree();
        }

        #region TOOLBAR
        /// <summary>
        ///     Open or add local repository.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenOrAddRepo(object sender, RoutedEventArgs e) {
            FolderDailog.Open("Open or init local repository", path => {
                CheckAndOpenRepo(path);
            });
        }

        /// <summary>
        ///     Clone remote repository.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloneRepo(object sender, RoutedEventArgs e) {
            if (MakeSureReady()) {
                popupManager.Show(new Clone(popupManager, () => {
                    UpdateTree();
                }));
            }
        }
        #endregion

        #region EVENT_TREEVIEW
        private void TreeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var addFolder = new MenuItem();
            addFolder.Header = "Add Folder";
            addFolder.Click += (o, ev) => {
                var group = App.Setting.AddGroup("New Group", "");
                UpdateTree(group.Id);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(addFolder);
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void TreeMouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (repositories.SelectedItem == null) return;
            DragDrop.DoDragDrop(repositories, repositories.SelectedItem, DragDropEffects.Move);
            e.Handled = true;
        }

        private void TreeDrop(object sender, DragEventArgs e) {
            bool needRebuild = false;

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                if (!MakeSureReady()) return;

                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                string group = "";

                var node = (sender as TreeViewItem)?.DataContext as Node;
                if (node != null) group = node.IsRepo ? node.ParentId : node.Id;

                foreach (var path in paths) {
                    FileInfo info = new FileInfo(path);
                    if (info.Attributes == FileAttributes.Directory && Git.Repository.IsValid(path)) {
                        App.Setting.AddRepository(path, group);
                        needRebuild = true;
                    }
                }
            } else if (e.Data.GetDataPresent(typeof(Node))) {
                var node = e.Data.GetData(typeof(Node)) as Node;
                if (node == null) return;

                var to = (sender as TreeViewItem)?.DataContext as Node;
                var parent = to != null ? to.Id : "";

                if (node.IsRepo) {
                    App.Setting.FindRepository(node.Id).GroupId = to != null ? to.Id : "";
                } else if (!App.Setting.IsSubGroup(node.Id, parent)) {
                    App.Setting.FindGroup(node.Id).ParentId = parent;
                }

                needRebuild = true;
            }

            if (needRebuild) UpdateTree();
            e.Handled = true;
        }
        #endregion

        #region EVENT_TREEVIEWITEM
        private void TreeNodeDoubleClick(object sender, MouseButtonEventArgs e) {
            var node = (sender as TreeViewItem).DataContext as Node;
            if (node != null && node.IsRepo) {
                CheckAndOpenRepo(node.Id);
                e.Handled = true;
            }
        }

        private void TreeNodeDragOver(object sender, DragEventArgs e) {
            var item = sender as TreeViewItem;
            var node = item.DataContext as Node;
            if (node != null && !node.IsRepo) item.IsExpanded = true;
            e.Handled = true;
        }

        private void TreeNodeDrop(object sender, DragEventArgs e) {
            TreeDrop(sender, e);
        }

        private void TreeNodeIsExpandedChanged(object sender, RoutedEventArgs e) {
            var item = sender as TreeViewItem;
            var node = item.DataContext as Node;

            if (node != null && !node.IsRepo) {
                var group = App.Setting.FindGroup(node.Id);
                group.IsExpended = item.IsExpanded;
                e.Handled = true;
            }
        }

        private void TreeNodeKeyDown(object sender, KeyEventArgs e) {
            if (e.Key != Key.Delete) return;

            var node = (sender as TreeViewItem).DataContext as Node;
            if (node != null) DeleteNode(node);
            e.Handled = true;
        }

        private void TreeNodeRenameStart(object sender, RoutedEventArgs e) {
            var text = sender as TextBox;
            if (text.IsVisible) {
                text.SelectAll();
                text.Focus();
            }
            e.Handled = true;
        }

        private void TreeNodeRenameKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                UpdateTree();
                e.Handled = true;
            } else if (e.Key == Key.Enter) {
                TreeNodeRenameEnd(sender, e);
                e.Handled = true;
            }
        }

        private void TreeNodeRenameEnd(object sender, RoutedEventArgs e) {
            var text = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text.Text)) {
                UpdateTree();
                e.Handled = false;
                return;
            }

            var node = text.DataContext as Node;
            if (node != null) {
                if (node.IsRepo) {
                    App.Setting.RenameRepository(node.Id, text.Text);
                } else {
                    App.Setting.RenameGroup(node.Id, text.Text);
                }

                UpdateTree();
                e.Handled = true;
            }
        }

        private void TreeNodeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var item = sender as TreeViewItem;
            var node = item.DataContext as Node;
            var menu = new ContextMenu();

            if (node.IsRepo) {
                var open = new MenuItem();
                open.Header = "Open";
                open.Click += (o, ev) => {
                    CheckAndOpenRepo(node.Id);
                    ev.Handled = true;
                };

                var explore = new MenuItem();
                explore.Header = "Open Container Folder";
                explore.Click += (o, ev) => {
                    Process.Start("explorer", node.Id);
                    ev.Handled = true;
                };

                var iconBookmark = FindResource("Icon.Bookmark") as Geometry;
                var bookmark = new MenuItem();
                bookmark.Header = "Bookmark";
                for (int i = 0; i < Converters.IntToRepoColor.Colors.Length; i++) {
                    var icon = new System.Windows.Shapes.Path();
                    icon.Style = FindResource("Style.Icon") as Style;
                    icon.Data = iconBookmark;
                    icon.Fill = Converters.IntToRepoColor.Colors[i];
                    icon.Width = 8;

                    var mark = new MenuItem();
                    mark.Icon = icon;
                    mark.Header = $"{i}";

                    var refIdx = i;
                    mark.Click += (o, ev) => {
                        var repo = App.Setting.FindRepository(node.Id);
                        if (repo != null) {
                            repo.Color = refIdx;
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
                addSubFolder.Header = "Add Sub-Folder";
                addSubFolder.Click += (o, ev) => {
                    var parent = App.Setting.FindGroup(node.Id);
                    if (parent != null) parent.IsExpended = true;

                    var group = App.Setting.AddGroup("New Group", node.Id);
                    UpdateTree(group.Id);
                    ev.Handled = true;
                };

                menu.Items.Add(addSubFolder);
            }

            var rename = new MenuItem();
            rename.Header = "Rename";
            rename.Click += (o, ev) => {
                UpdateTree(node.Id);
                ev.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = "Delete";
            delete.Click += (o, ev) => {
                DeleteNode(node);
                ev.Handled = true;
            };

            menu.Items.Add(rename);
            menu.Items.Add(delete);
            menu.IsOpen = true;
            e.Handled = true;
        }
        #endregion

        #region PRIVATES
        /// <summary>
        ///     Make sure git is configured.
        /// </summary>
        /// <returns></returns>
        private bool MakeSureReady() {
            if (!App.IsGitConfigured) {
                App.RaiseError("Git has NOT been configured.\nPlease to go [Preference] and configure it first.");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Check and open repository
        /// </summary>
        /// <param name="path"></param>
        private void CheckAndOpenRepo(string path) {
            if (!MakeSureReady()) return;

            if (!Git.Repository.IsValid(path)) {
                if (Directory.Exists(path)) {
                    popupManager.Show(new Init(popupManager, path, () => UpdateTree()));
                    return;
                }

                App.RaiseError($"Path[{path}] not exists!");
                return;
            }

            var repo = App.Setting.AddRepository(path, "");
            App.Open(repo);
        }

        /// <summary>
        ///     Update tree items.
        /// </summary>
        /// <param name="editingNodeId"></param>
        private void UpdateTree(string editingNodeId = null) {
            var groupNodes = new Dictionary<string, Node>();
            var nodes = new List<Node>();

            foreach (var group in App.Setting.Groups) {
                Node node = new Node() {
                    Id = group.Id,
                    ParentId = group.ParentId,
                    Name = group.Name,
                    IsRepo = false,
                    IsExpended = group.IsExpended,
                    IsEditing = group.Id == editingNodeId,
                    Color = 0,
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

            foreach (var repo in App.Setting.Repositories) {
                Node node = new Node() {
                    Id = repo.Path,
                    ParentId = repo.GroupId,
                    Name = repo.Name,
                    IsRepo = true,
                    IsExpended = false,
                    IsEditing = repo.Path == editingNodeId,
                    Color = repo.Color,
                };

                if (groupNodes.ContainsKey(repo.GroupId)) {
                    groupNodes[repo.GroupId].Children.Add(node);
                } else {
                    nodes.Add(node);
                }
            }

            repositories.ItemsSource = nodes;
        }

        /// <summary>
        ///     Delete tree node.
        /// </summary>
        /// <param name="node"></param>
        private void DeleteNode(Node node) {
            if (node.IsRepo) {
                App.Setting.RemoveRepository(node.Id);
            } else {
                App.Setting.RemoveGroup(node.Id);
            }

            UpdateTree();
        }
        #endregion
    }
}
