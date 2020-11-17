using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SourceGit.UI {

    /// <summary>
    ///     Repository manager.
    /// </summary>
    public partial class Manager : UserControl {
        private TreeViewItem selectedTreeViewItem = null;

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
            public List<Node> Children { get; set; } = new List<Node>();
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public Manager() {
            InitializeComponent();
            UpdateRecentOpened();
            UpdateTree();
        }

        #region TOOLBAR
        /// <summary>
        ///     Open or add local repository.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenOrAddRepo(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Open or init local repository";
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                CheckAndOpenRepo(dialog.SelectedPath);
            }
        }

        /// <summary>
        ///     Clone remote repository.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloneRepo(object sender, RoutedEventArgs e) {
            if (MakeSureReady()) popupManager.Show(new Clone(popupManager));
        }
        #endregion

        #region EVENT_RECENT_LISTVIEW
        private void RecentsGotFocus(object sender, RoutedEventArgs e) {
            if (selectedTreeViewItem != null) selectedTreeViewItem.IsSelected = false;
            selectedTreeViewItem = null;
            e.Handled = true;
        }

        private void RecentsSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var recent = recentOpened.SelectedItem as Git.Repository;
            if (recent != null) ShowBrief(recent);
            e.Handled = true;
        }

        private void RecentsMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var list = sender as ListView;
            var recent = list.SelectedItem as Git.Repository;

            if (recent != null) {
                CheckAndOpenRepo(recent.Path);
                e.Handled = true;
            }
        }

        private void RecentsContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var repo = (sender as ListViewItem).DataContext as Git.Repository;
            if (repo == null) return;

            var open = new MenuItem();
            open.Header = "Open";
            open.Click += (o, ev) => {
                CheckAndOpenRepo(repo.Path);
                ev.Handled = true;
            };

            var explore = new MenuItem();
            explore.Header = "Open Container Folder";
            explore.Click += (o, ev) => {
                Process.Start("explorer", repo.Path);
                ev.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = "Delete";
            delete.Click += (o, ev) => {
                App.Preference.RemoveRepository(repo.Path);
                UpdateRecentOpened();
                UpdateTree();
                HideBrief();
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(open);
            menu.Items.Add(explore);
            menu.Items.Add(delete);
            menu.IsOpen = true;
            e.Handled = true;
        }
        #endregion

        #region EVENT_TREEVIEW
        private void TreeGotFocus(object sender, RoutedEventArgs e) {
            recentOpened.SelectedItems.Clear();
            e.Handled = true;
        }

        private void TreeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var addFolder = new MenuItem();
            addFolder.Header = "Add Folder";
            addFolder.Click += (o, ev) => {
                var group = App.Preference.AddGroup("New Group", "");
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

            if (selectedTreeViewItem == null) return;

            var node = selectedTreeViewItem.DataContext as Node;
            if (node == null || !node.IsRepo) return;

            DragDrop.DoDragDrop(repositories, selectedTreeViewItem, DragDropEffects.Move);
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
                        App.Preference.AddRepository(path, group);
                        needRebuild = true;
                    }
                }
            } else if (e.Data.GetDataPresent(typeof(TreeViewItem))) {
                var item = e.Data.GetData(typeof(TreeViewItem)) as TreeViewItem;
                var node = item.DataContext as Node;
                if (node == null || !node.IsRepo) return;

                var group = "";
                var to = (sender as TreeViewItem)?.DataContext as Node;
                if (to != null) group = to.IsRepo ? to.ParentId : to.Id;
                App.Preference.FindRepository(node.Id).GroupId = group;
                needRebuild = true;
            }

            if (needRebuild) UpdateTree();
            e.Handled = true;
        }
        #endregion

        #region EVENT_TREEVIEWITEM
        private void TreeNodeSelected(object sender, RoutedEventArgs e) {
            selectedTreeViewItem = sender as TreeViewItem;

            var node = selectedTreeViewItem.DataContext as Node;
            if (node.IsRepo) {
                ShowBrief(App.Preference.FindRepository(node.Id));
            } else {
                HideBrief();
            }

            e.Handled = true;
        }

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
                var group = App.Preference.FindGroup(node.Id);
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
                    App.Preference.RenameRepository(node.Id, text.Text);
                } else {
                    App.Preference.RenameGroup(node.Id, text.Text);
                }

                UpdateRecentOpened();
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

                menu.Items.Add(open);
                menu.Items.Add(explore);
            } else {
                var addSubFolder = new MenuItem();
                addSubFolder.Header = "Add Sub-Folder";
                addSubFolder.Click += (o, ev) => {
                    var parent = App.Preference.FindGroup(node.Id);
                    if (parent != null) parent.IsExpended = true;

                    var group = App.Preference.AddGroup("New Group", node.Id);
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
                HideBrief();
                ev.Handled = true;
            };

            menu.Items.Add(rename);
            menu.Items.Add(delete);
            menu.IsOpen = true;
            e.Handled = true;
        }
        #endregion

        #region EVENT_BRIEF
        private void ShowBrief(Git.Repository repo) {
            if (repo == null || !Git.Repository.IsValid(repo.Path)) {
                if (Directory.Exists(repo.Path)) {
                    popupManager.Show(new Init(popupManager, repo.Path));
                } else {
                    App.RaiseError("Path is NOT valid git repository or has been removed.");
                    App.Preference.RemoveRepository(repo.Path);
                    UpdateRecentOpened();
                    UpdateTree();
                }
                
                return;
            }

            briefMask.Visibility = Visibility.Hidden;

            repoName.Content = repo.Name;
            repoPath.Content = repo.Path;

            Task.Run(() => {
                var changes = repo.LocalChanges();
                var count = changes.Count;
                Dispatcher.Invoke(() => localChanges.Content = count);
            });

            Task.Run(() => {
                var count = repo.TotalCommits();
                Dispatcher.Invoke(() => totalCommits.Content = count);
            });

            Task.Run(() => {
                var commits = repo.Commits("-n 1");
                Dispatcher.Invoke(() => {
                    if (commits.Count > 0) {
                        var c = commits[0];
                        lastCommitId.Content = c.ShortSHA;
                        lastCommit.Content = c.Subject;
                    } else {
                        lastCommitId.Content = "---";
                        lastCommit.Content = "";
                    }
                });
            });

            if (File.Exists(repo.Path + "/README.md")) {
                readme.Text = File.ReadAllText(repo.Path + "/README.md");
            } else {
                readme.Text = "";
            }
        }

        private void HideBrief() {
            briefMask.Visibility = Visibility.Visible;
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
                    popupManager.Show(new Init(popupManager, path));
                    return;
                }

                App.RaiseError($"Path[{path}] not exists!");
                return;
            }

            var repo = App.Preference.AddRepository(path, "");
            if (!repo.BringUpTab()) repo.Open();
        }

        /// <summary>
        ///     Update recent opened repositories.
        /// </summary>
        private void UpdateRecentOpened() {
            var sorted = App.Preference.Repositories.OrderByDescending(a => a.LastOpenTime).ToList();
            var top5 = new List<Git.Repository>();

            for (int i = 0; i < sorted.Count && i < 5; i++) {
                if (sorted[i].LastOpenTime <= 0) break;
                top5.Add(sorted[i]);
            }

            recentOpened.ItemsSource = top5;
        }

        /// <summary>
        ///     Update tree items.
        /// </summary>
        /// <param name="editingNodeId"></param>
        private void UpdateTree(string editingNodeId = null) {
            var groupNodes = new Dictionary<string, Node>();
            var nodes = new List<Node>();

            foreach (var group in App.Preference.Groups) {
                Node node = new Node() {
                    Id = group.Id,
                    ParentId = group.ParentId,
                    Name = group.Name,
                    IsRepo = false,
                    IsExpended = group.IsExpended,
                    IsEditing = group.Id == editingNodeId,
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

            foreach (var repo in App.Preference.Repositories) {
                Node node = new Node() {
                    Id = repo.Path,
                    ParentId = repo.GroupId,
                    Name = repo.Name,
                    IsRepo = true,
                    IsExpended = false,
                    IsEditing = repo.Path == editingNodeId,
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
                App.Preference.RemoveRepository(node.Id);
                UpdateRecentOpened();
            } else {
                App.Preference.RemoveGroup(node.Id);
            }

            UpdateTree();
        }
        #endregion
    }
}
