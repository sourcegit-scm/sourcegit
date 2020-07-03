using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace SourceGit.UI {

    /// <summary>
    ///     Commit detail viewer
    /// </summary>
    public partial class CommitViewer : UserControl {
        private Git.Repository repo = null;
        private Git.Commit commit = null;
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
            public List<Node> Children { get; set; } = new List<Node>();
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public CommitViewer() {
            InitializeComponent();
        }

        #region DATA
        public void SetData(Git.Repository opened, Git.Commit selected) {
            repo = opened;
            commit = selected;

            SetBaseInfo(commit);

            Task.Run(() => {
                cachedChanges.Clear();
                cachedChanges = commit.GetChanges(repo);

                Dispatcher.Invoke(() => {
                    changeList1.ItemsSource = null;
                    changeList1.ItemsSource = cachedChanges;
                });

                LayoutChanges();
                SetRevisionFiles(commit.GetFiles(repo));
            });
        }

        private void Cleanup(object sender, RoutedEventArgs e) {
            fileTree.ItemsSource = null;
            changeList1.ItemsSource = null;
            changeList2.ItemsSource = null;
            displayChanges.Clear();
            cachedChanges.Clear();
            diffViewer.Reset();
        }
        #endregion

        #region BASE_INFO
        private void SetBaseInfo(Git.Commit commit) {
            var parentIds = new List<string>();
            foreach (var p in commit.Parents) parentIds.Add(p.Substring(0, 8));

            SHA.Text = commit.SHA;
            refs.ItemsSource = commit.Decorators;
            parents.ItemsSource = parentIds;
            author.Text = $"{commit.Author.Name} <{commit.Author.Email}>";
            authorTime.Text = commit.Author.Time;
            committer.Text = $"{commit.Committer.Name} <{commit.Committer.Email}>";
            committerTime.Text = commit.Committer.Time;
            subject.Text = commit.Subject;
            message.Text = commit.Message.Trim();

            if (commit.Decorators.Count == 0) lblRefs.Visibility = Visibility.Collapsed;
            else lblRefs.Visibility = Visibility.Visible;

            if (commit.Committer.Email == commit.Author.Email && commit.Committer.Time == commit.Author.Time) {
                committerRow.Height = new GridLength(0);
            } else {
                committerRow.Height = GridLength.Auto;
            }
        }

        private void NavigateParent(object sender, RequestNavigateEventArgs e) {
            repo.OnNavigateCommit?.Invoke(e.Uri.OriginalString);
            e.Handled = true;
        }

        #endregion

        #region CHANGES
        private void LayoutChanges() {
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
            Task.Run(() => LayoutChanges());
        }

        private async void ChangeTreeItemSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            diffViewer.Reset();

            var node = e.NewValue as Node;
            if (node == null || !node.IsFile) return;

            var start = $"{commit.SHA}^";
            if (commit.Parents.Count == 0) {
                start = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
            }

            List<string> data = new List<string>();

            await Task.Run(() => {
                data = repo.Diff(start, commit.SHA, node.FilePath, node.OriginalPath);
            });

            diffViewer.SetData(data, node.FilePath, node.OriginalPath);
        }

        private async void ChangeListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var change = e.AddedItems[0] as Git.Change;
            if (change == null) return;

            var start = $"{commit.SHA}^";
            if (commit.Parents.Count == 0) {
                start = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
            }

            List<string> data = new List<string>();

            await Task.Run(() => {
                data = repo.Diff(start, commit.SHA, change.Path, change.OriginalPath);
            });

            diffViewer.SetData(data, change.Path, change.OriginalPath);
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

                MenuItem blame = new MenuItem();
                blame.Header = "Blame";
                blame.Click += (obj, ev) => {
                    Blame viewer = new Blame(repo, path, commit.SHA);
                    viewer.Show();
                };
                menu.Items.Add(blame);

                MenuItem explore = new MenuItem();
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, ev) => {
                    var absPath = Path.GetFullPath(repo.Path + "\\" + path);
                    Process.Start("explorer", $"/select,{absPath}");
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                MenuItem saveAs = new MenuItem();
                saveAs.Header = "Save As ...";
                saveAs.Click += (obj, ev) => {
                    var dialog = new System.Windows.Forms.FolderBrowserDialog();
                    dialog.Description = change.Path;
                    dialog.ShowNewFolderButton = true;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                        var savePath = Path.Combine(dialog.SelectedPath, Path.GetFileName(path));
                        repo.RunAndRedirect($"show {commit.SHA}:\"{path}\"", savePath);
                    }
                };
                menu.Items.Add(saveAs);
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
        #endregion

        #region FILES
        private void SetRevisionFiles(List<string> files) {
            List<Node> fileTreeSource = new List<Node>();
            Dictionary<string, Node> folders = new Dictionary<string, Node>();

            foreach (var path in files) {
                var sepIdx = path.IndexOf("/");
                if (sepIdx == -1) {
                    Node node = new Node();
                    node.FilePath = path;
                    node.Name = path;
                    node.IsFile = true;
                    node.IsNodeExpanded = false;
                    fileTreeSource.Add(node);
                } else {
                    Node lastFolder = null;
                    var start = 0;

                    while (sepIdx != -1) {
                        var folder = path.Substring(0, sepIdx);
                        if (folders.ContainsKey(folder)) {
                            lastFolder = folders[folder];
                        } else if (lastFolder == null) {
                            lastFolder = new Node();
                            lastFolder.FilePath = folder;
                            lastFolder.Name = folder.Substring(start);
                            lastFolder.IsNodeExpanded = false;
                            fileTreeSource.Add(lastFolder);
                            folders.Add(folder, lastFolder);
                        } else {
                            var folderNode = new Node();
                            folderNode.FilePath = folder;
                            folderNode.Name = folder.Substring(start);
                            folderNode.IsNodeExpanded = false;
                            folders.Add(folder, folderNode);
                            lastFolder.Children.Add(folderNode);
                            lastFolder = folderNode;
                        }

                        start = sepIdx + 1;
                        sepIdx = path.IndexOf('/', start);
                    }

                    Node node = new Node();
                    node.FilePath = path;
                    node.Name = path.Substring(start);
                    node.IsFile = true;
                    node.IsNodeExpanded = false;
                    lastFolder.Children.Add(node);
                }
            }

            folders.Clear();
            SortTreeNodes(fileTreeSource);

            Dispatcher.Invoke(() => {
                fileTree.ItemsSource = fileTreeSource;
                filePreview.Text = "";
            });
        }

        private async void FileTreeItemSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            filePreview.Text = "";

            var node = e.NewValue as Node;
            if (node == null || !node.IsFile) return;

            await Task.Run(() => {
                var data = commit.GetTextFileContent(repo, node.FilePath);
                Dispatcher.Invoke(() => filePreview.Text = data);
            });
        }
        #endregion

        #region TREE_COMMON
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

                MenuItem blame = new MenuItem();
                blame.Header = "Blame";
                blame.Click += (obj, ev) => {
                    Blame viewer = new Blame(repo, node.FilePath, commit.SHA);
                    viewer.Show();
                };
                menu.Items.Add(blame);

                MenuItem explore = new MenuItem();
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, ev) => {
                    var path = Path.GetFullPath(repo.Path + "\\" + node.FilePath);
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                MenuItem saveAs = new MenuItem();
                saveAs.Header = "Save As ...";
                saveAs.Click += (obj, ev) => {
                    var dialog = new System.Windows.Forms.FolderBrowserDialog();
                    dialog.Description = node.FilePath;
                    dialog.ShowNewFolderButton = true;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                        var path = Path.Combine(dialog.SelectedPath, node.Name);
                        repo.RunAndRedirect($"show {commit.SHA}:\"{node.FilePath}\"", path);
                    }
                };
                menu.Items.Add(saveAs);
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
        #endregion
    }
}
