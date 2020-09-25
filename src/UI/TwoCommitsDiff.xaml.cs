using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Diff with selected 2 commits.
    /// </summary>
    public partial class TwoCommitsDiff : UserControl {
        private Git.Repository repo = null;
        private string sha1 = null;
        private string sha2 = null;
        private List<Git.Change> cachedChanges = new List<Git.Change>();
        private List<Git.Change> displayChanges = new List<Git.Change>();
        private string changeFilter = null;

        /// <summary>
        ///     Node for file tree.
        /// </summary>
        public class Node {
            public string FilePath { get; set; } = "";
            public string OriginalPath { get; set; } = "";
            public string Name { get; set; } = "";
            public bool IsFile { get; set; } = false;
            public bool IsNodeExpanded { get; set; } = true;
            public Git.Change Change { get; set; } = null;
            public Git.Commit.Object CommitObject { get; set; } = null;
            public List<Node> Children { get; set; } = new List<Node>();
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public TwoCommitsDiff() {
            InitializeComponent();
        }

        /// <summary>
        ///     Show.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="sha1"></param>
        /// <param name="sha2"></param>
        public void SetData(Git.Repository repo, string sha1, string sha2) {
            this.repo = repo;
            this.sha1 = sha1;
            this.sha2 = sha2;

            txtTitle.Content = $"COMMIT: {sha1} -> {sha2}";
            Task.Run(() => LoadChanges(true));
        }

        /// <summary>
        ///     Cleanup.
        /// </summary>
        public void Cleanup() {
            repo = null;
            cachedChanges.Clear();
            displayChanges.Clear();
        }

        private void LoadChanges(bool reload = false) {
            if (reload) {
                cachedChanges.Clear();

                repo.RunCommand($"diff --name-status {sha1} {sha2}", line => {
                    var c = Git.Change.Parse(line, true);
                    if (c != null) cachedChanges.Add(c);
                });
            }            

            displayChanges.Clear();

            if (string.IsNullOrEmpty(changeFilter)) {
                displayChanges.AddRange(cachedChanges);
            } else {
                foreach (var c in cachedChanges) {
                    if (c.Path.ToUpper().Contains(changeFilter)) displayChanges.Add(c);
                }
            }

            List<Node> changeTreeSource = new List<Node>();
            Dictionary<string, Node> folders = new Dictionary<string, Node>();
            bool isDefaultExpanded = displayChanges.Count < 50;

            foreach (var c in displayChanges) {
                var sepIdx = c.Path.IndexOf('/');
                if (sepIdx == -1) {
                    Node node = new Node();
                    node.FilePath = c.Path;
                    node.IsFile = true;
                    node.Name = c.Path;
                    node.Change = c;
                    node.IsNodeExpanded = isDefaultExpanded;
                    if (c.OriginalPath != null) node.OriginalPath = c.OriginalPath;
                    changeTreeSource.Add(node);
                } else {
                    Node lastFolder = null;
                    var start = 0;

                    while (sepIdx != -1) {
                        var folder = c.Path.Substring(0, sepIdx);
                        if (folders.ContainsKey(folder)) {
                            lastFolder = folders[folder];
                        } else if (lastFolder == null) {
                            lastFolder = new Node();
                            lastFolder.FilePath = folder;
                            lastFolder.Name = folder.Substring(start);
                            lastFolder.IsNodeExpanded = isDefaultExpanded;
                            changeTreeSource.Add(lastFolder);
                            folders.Add(folder, lastFolder);
                        } else {
                            var folderNode = new Node();
                            folderNode.FilePath = folder;
                            folderNode.Name = folder.Substring(start);
                            folderNode.IsNodeExpanded = isDefaultExpanded;
                            folders.Add(folder, folderNode);
                            lastFolder.Children.Add(folderNode);
                            lastFolder = folderNode;
                        }

                        start = sepIdx + 1;
                        sepIdx = c.Path.IndexOf('/', start);
                    }

                    Node node = new Node();
                    node.FilePath = c.Path;
                    node.Name = c.Path.Substring(start);
                    node.IsFile = true;
                    node.Change = c;
                    if (c.OriginalPath != null) node.OriginalPath = c.OriginalPath;
                    lastFolder.Children.Add(node);
                }
            }

            folders.Clear();
            SortTreeNodes(changeTreeSource);

            Dispatcher.Invoke(() => {
                changeList2.ItemsSource = null;
                changeList2.ItemsSource = displayChanges;
                changeTree.ItemsSource = changeTreeSource;
                diffViewer.Reset();
            });
        }

        private void SearchChangeFileTextChanged(object sender, TextChangedEventArgs e) {
            changeFilter = txtChangeFilter.Text.ToUpper();
            Task.Run(() => LoadChanges());
        }

        private void ChangeTreeItemSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            diffViewer.Reset();

            var node = e.NewValue as Node;
            if (node == null || !node.IsFile) return;

            diffViewer.Diff(repo, new DiffViewer.Option() {
                RevisionRange = new string[] { sha1, sha2 },
                Path = node.FilePath,
                OrgPath = node.OriginalPath
            });
        }

        private void ChangeListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var change = e.AddedItems[0] as Git.Change;
            if (change == null) return;

            diffViewer.Diff(repo, new DiffViewer.Option() {
                RevisionRange = new string[] { sha1, sha2 },
                Path = change.Path,
                OrgPath = change.OriginalPath
            });
        }

        private void ChangeListContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var row = sender as DataGridRow;
            if (row == null) return;

            var change = row.DataContext as Git.Change;
            if (change == null) return;

            var path = change.Path;
            var menu = new ContextMenu();
            if (change.Index != Git.Change.Status.Deleted) {
                MenuItem history = new MenuItem();
                history.Header = "File History";
                history.Click += (o, ev) => {
                    var viewer = new FileHistories(repo, path);
                    viewer.Show();
                };
                menu.Items.Add(history);

                MenuItem explore = new MenuItem();
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, ev) => {
                    var absPath = Path.GetFullPath(repo.Path + "\\" + path);
                    Process.Start("explorer", $"/select,{absPath}");
                    e.Handled = true;
                };
                menu.Items.Add(explore);
            }

            MenuItem copyPath = new MenuItem();
            copyPath.Header = "Copy Path";
            copyPath.Click += (obj, ev) => {
                Clipboard.SetText(path);
            };
            menu.Items.Add(copyPath);
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void ChangeListMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var row = sender as DataGridRow;
            if (row == null) return;

            var change = row.DataContext as Git.Change;
            if (change == null) return;

            var viewer = new FileHistories(repo, change.Path);
            viewer.Show();
        }

        private void SortTreeNodes(List<Node> list) {
            list.Sort((l, r) => {
                if (l.IsFile) {
                    return r.IsFile ? l.Name.CompareTo(r.Name) : 1;
                } else {
                    return r.IsFile ? -1 : l.Name.CompareTo(r.Name);
                }
            });

            foreach (var sub in list) {
                if (sub.Children.Count > 0) SortTreeNodes(sub.Children);
            }
        }

        private ScrollViewer GetScrollViewer(FrameworkElement owner) {
            if (owner == null) return null;
            if (owner is ScrollViewer) return owner as ScrollViewer;

            int n = VisualTreeHelper.GetChildrenCount(owner);
            for (int i = 0; i < n; i++) {
                var child = VisualTreeHelper.GetChild(owner, i) as FrameworkElement;
                var deep = GetScrollViewer(child);
                if (deep != null) return deep;
            }

            return null;
        }

        private void TreeMouseWheel(object sender, MouseWheelEventArgs e) {
            var scroll = GetScrollViewer(sender as TreeView);
            if (scroll == null) return;

            if (e.Delta > 0) {
                scroll.LineUp();
            } else {
                scroll.LineDown();
            }

            e.Handled = true;
        }

        private void TreeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var item = sender as TreeViewItem;
            if (item == null) return;

            var node = item.DataContext as Node;
            if (node == null || !node.IsFile) return;

            item.IsSelected = true;

            ContextMenu menu = new ContextMenu();
            if (node.Change == null || node.Change.Index != Git.Change.Status.Deleted) {
                MenuItem history = new MenuItem();
                history.Header = "File History";
                history.Click += (o, ev) => {
                    var viewer = new FileHistories(repo, node.FilePath);
                    viewer.Show();
                };
                menu.Items.Add(history);

                MenuItem explore = new MenuItem();
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, ev) => {
                    var path = Path.GetFullPath(repo.Path + "\\" + node.FilePath);
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };
                menu.Items.Add(explore);
            }

            MenuItem copyPath = new MenuItem();
            copyPath.Header = "Copy Path";
            copyPath.Click += (obj, ev) => {
                Clipboard.SetText(node.FilePath);
            };
            menu.Items.Add(copyPath);

            menu.IsOpen = true;
            e.Handled = true;
        }
    }
}
