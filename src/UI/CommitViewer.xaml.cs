using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            public Git.Commit.Object CommitObject { get; set; } = null;
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
            subject.Text = commit.Subject;

            var commitMsg = commit.Message.Trim();
            if (string.IsNullOrEmpty(commitMsg)) {
                descRow.Height = new GridLength(0);
            } else {
                descRow.Height = GridLength.Auto;
                message.Text = commitMsg;
            }

            authorName.Text = commit.Author.Name;
            authorEmail.Text = commit.Author.Email;
            authorTime.Text = commit.Author.Time;
            authorAvatar.User = commit.Author;

            committerName.Text = commit.Committer.Name;
            committerEmail.Text = commit.Committer.Email;
            committerTime.Text = commit.Committer.Time;
            committerAvatar.User = commit.Committer;

            if (commit.Committer.Email == commit.Author.Email && commit.Committer.Time == commit.Author.Time) {
                committerPanel.Visibility = Visibility.Hidden;
            } else {
                committerPanel.Visibility = Visibility.Visible;
            }

            if (commit.Decorators.Count == 0) {
                refRow.Height = new GridLength(0);
            } else {
                refRow.Height = GridLength.Auto;
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

        private void ChangeTreeItemSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            diffViewer.Reset();

            var node = e.NewValue as Node;
            if (node == null || !node.IsFile) return;

            var start = $"{commit.SHA}^";
            if (commit.Parents.Count == 0) {
                start = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
            }

            diffViewer.Diff(repo, new DiffViewer.Option() {
                RevisionRange = new string[] { start, commit.SHA },
                Path = node.FilePath,
                OrgPath = node.OriginalPath
            });
        }

        private void ChangeListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1) return;

            var change = e.AddedItems[0] as Git.Change;
            if (change == null) return;

            var start = $"{commit.SHA}^";
            if (commit.Parents.Count == 0) {
                start = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
            }

            diffViewer.Diff(repo, new DiffViewer.Option() {
                RevisionRange = new string[] { start, commit.SHA },
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
                history.Header = App.Text("FileHistory");
                history.Click += (o, ev) => {
                    var viewer = new FileHistories(repo, path);
                    viewer.Show();
                };
                menu.Items.Add(history);

                MenuItem blame = new MenuItem();
                blame.Header = App.Text("Blame");
                blame.Click += (obj, ev) => {
                    Blame viewer = new Blame(repo, path, commit.SHA);
                    viewer.Show();
                };
                menu.Items.Add(blame);

                MenuItem explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, ev) => {
                    var absPath = Path.GetFullPath(repo.Path + "\\" + path);
                    Process.Start("explorer", $"/select,{absPath}");
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                MenuItem saveAs = new MenuItem();
                saveAs.Header = App.Text("SaveAs");
                saveAs.Click += (obj, ev) => {
                    FolderDialog.Open(App.Text("SaveFileTo"), saveTo => {
                        var savePath = Path.Combine(saveTo, Path.GetFileName(path));
                        commit.SaveFileTo(repo, path, savePath);
                    });
                };
                menu.Items.Add(saveAs);
            }

            MenuItem copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
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
        #endregion

        #region FILES
        private void SetRevisionFiles(List<Git.Commit.Object> files) {
            List<Node> fileTreeSource = new List<Node>();
            Dictionary<string, Node> folders = new Dictionary<string, Node>();

            foreach (var obj in files) {
                var sepIdx = obj.Path.IndexOf("/");
                if (sepIdx == -1) {
                    Node node = new Node();
                    node.FilePath = obj.Path;
                    node.Name = obj.Path;
                    node.IsFile = true;
                    node.IsNodeExpanded = false;
                    node.CommitObject = obj;
                    fileTreeSource.Add(node);
                } else {
                    Node lastFolder = null;
                    var start = 0;

                    while (sepIdx != -1) {
                        var folder = obj.Path.Substring(0, sepIdx);
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
                        sepIdx = obj.Path.IndexOf('/', start);
                    }

                    Node node = new Node();
                    node.FilePath = obj.Path;
                    node.Name = obj.Path.Substring(start);
                    node.IsFile = true;
                    node.IsNodeExpanded = false;
                    node.CommitObject = obj;
                    lastFolder.Children.Add(node);
                }
            }

            folders.Clear();
            SortTreeNodes(fileTreeSource);

            Dispatcher.Invoke(() => {
                fileTree.ItemsSource = fileTreeSource;
                previewEditor.Children.Clear();
            });
        }

        private void LayoutPreview(List<Git.Commit.Line> data) {
            var maxLineNumber = $"{data.Count + 1}";
            var formatted = new FormattedText(
                maxLineNumber,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                12.0,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var grid = new DataGrid();
            grid.SetValue(Grid.RowProperty, 1);
            grid.RowHeight = 16.0;
            grid.FrozenColumnCount = 1;
            grid.ContextMenuOpening += OnPreviewContextMenuOpening;
            grid.RowStyle = FindResource("Style.DataGridRow.NoBringIntoView") as Style;

            var colLineNumber = new DataGridTextColumn();
            colLineNumber.IsReadOnly = true;
            colLineNumber.Binding = new Binding("No");
            colLineNumber.ElementStyle = FindResource("Style.DataGridText.LineNumber") as Style;
            colLineNumber.Width = new DataGridLength(formatted.Width + 16, DataGridLengthUnitType.Pixel);
            grid.Columns.Add(colLineNumber);

            var offset = formatted.Width + 16;
            if (data.Count * 16 > previewEditor.ActualHeight) offset += 8;

            var colContent = new DataGridTextColumn();
            colContent.IsReadOnly = true;
            colContent.Binding = new Binding("Content");
            colContent.ElementStyle = FindResource("Style.DataGridText.Content") as Style;
            colContent.MinWidth = previewEditor.ActualWidth - offset;
            colContent.Width = DataGridLength.SizeToCells;
            grid.Columns.Add(colContent);

            var splitter = new System.Windows.Shapes.Rectangle();
            splitter.Width = 1;
            splitter.Fill = FindResource("Brush.Border2") as Brush;
            splitter.HorizontalAlignment = HorizontalAlignment.Left;
            splitter.Margin = new Thickness(formatted.Width + 15, 0, 0, 0);

            grid.ItemsSource = data;
            previewEditor.Children.Add(grid);
            previewEditor.Children.Add(splitter);
        }

        private bool IsImage(string path) {
            return path.EndsWith(".png") || 
                path.EndsWith(".jpg") || 
                path.EndsWith(".jpeg") || 
                path.EndsWith(".ico") || 
                path.EndsWith(".bmp") ||
                path.EndsWith(".tiff") ||
                path.EndsWith(".gif");
        }

        private void OnPreviewContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var grid = sender as DataGrid;
            if (grid == null) return;

            var menu = new ContextMenu();

            var copyIcon = new System.Windows.Shapes.Path();
            copyIcon.Style = FindResource("Style.Icon") as Style;
            copyIcon.Data = FindResource("Icon.Copy") as Geometry;
            copyIcon.Width = 10;

            var copy = new MenuItem();
            copy.Header = "Copy";
            copy.Icon = copyIcon;
            copy.Click += (o, ev) => {
                var items = grid.SelectedItems;
                if (items.Count == 0) return;

                var builder = new StringBuilder();
                foreach (var item in items) {
                    var line = item as Git.Commit.Line;
                    if (line == null) continue;

                    builder.Append(line.Content);
                    builder.AppendLine();
                }

                Clipboard.SetText(builder.ToString());
            };
            menu.Items.Add(copy);
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OnPreviewRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        private void OnPreviewSizeChanged(object sender, SizeChangedEventArgs e) {
            if (previewEditor.Children.Count == 0) return;

            var totalWidth = previewEditor.ActualWidth;
            var totalHeight = previewEditor.ActualHeight;
            var editor = previewEditor.Children[0] as DataGrid;
            var minWidth = totalWidth - editor.NonFrozenColumnsViewportHorizontalOffset;
            var desireHeight = editor.Items.Count * editor.RowHeight;
            if (desireHeight > totalHeight) minWidth -= 8;

            editor.Columns[1].MinWidth = minWidth;
            editor.Columns[1].Width = DataGridLength.SizeToCells;
            editor.UpdateLayout();
        }

        private async void FileTreeItemSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            previewEditor.Children.Clear();
            previewImage.Visibility = Visibility.Collapsed;
            maskPreviewNotSupported.Visibility = Visibility.Collapsed;
            maskRevision.Visibility = Visibility.Collapsed;

            var node = e.NewValue as Node;
            if (node == null || !node.IsFile || node.CommitObject == null) return;

            switch (node.CommitObject.Kind) {
            case Git.Commit.Object.Type.Blob:
                if (IsImage(node.FilePath)) {
                    var tmp = Path.GetTempFileName();
                    commit.SaveFileTo(repo, node.FilePath, tmp);
                    previewImageData.Source = new BitmapImage(new Uri(tmp, UriKind.Absolute));
                    previewImage.Visibility = Visibility.Visible;
                } else if (repo.IsLFSFiltered(node.FilePath)) {
                    var obj = repo.GetLFSObject(commit.SHA, node.FilePath);
                    maskRevision.Visibility = Visibility.Visible;
                    iconPreviewRevision.Data = FindResource("Icon.LFS") as Geometry;
                    txtPreviewRevision.Content = "LFS SIZE:" + App.Format("Bytes", obj.Size);
                } else {
                    await Task.Run(() => {
                        var data = new List<Git.Commit.Line>();
                        var isBinary = commit.GetTextFileContent(repo, node.FilePath, data);

                        if (isBinary) {
                            Dispatcher.Invoke(() => maskPreviewNotSupported.Visibility = Visibility.Visible);
                        } else {
                            Dispatcher.Invoke(() => LayoutPreview(data));
                        }
                    });
                }
                break;
            case Git.Commit.Object.Type.Tag:
                maskRevision.Visibility = Visibility.Visible;
                iconPreviewRevision.Data = FindResource("Icon.Tag") as Geometry;
                txtPreviewRevision.Content = "TAG: " + node.CommitObject.SHA;
                break;
            case Git.Commit.Object.Type.Commit:
                maskRevision.Visibility = Visibility.Visible;
                iconPreviewRevision.Data = FindResource("Icon.Submodule") as Geometry;
                txtPreviewRevision.Content = "SUBMODULE: " + node.CommitObject.SHA;
                break;
            default:
                return;
            }            
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

        private void TreeMouseWheel(object sender, MouseWheelEventArgs e) {
            var scroll = Helpers.TreeViewHelper.GetScrollViewer(sender as TreeView);
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
                history.Header = App.Text("FileHistory");
                history.Click += (o, ev) => {
                    var viewer = new FileHistories(repo, node.FilePath);
                    viewer.Show();
                };
                menu.Items.Add(history);

                MenuItem blame = new MenuItem();
                blame.Header = App.Text("Blame");
                blame.Click += (obj, ev) => {
                    Blame viewer = new Blame(repo, node.FilePath, commit.SHA);
                    viewer.Show();
                };
                menu.Items.Add(blame);

                MenuItem explore = new MenuItem();
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, ev) => {
                    var path = Path.GetFullPath(repo.Path + "\\" + node.FilePath);
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };
                menu.Items.Add(explore);

                MenuItem saveAs = new MenuItem();
                saveAs.Header = App.Text("SaveAs");
                saveAs.IsEnabled = node.CommitObject == null || node.CommitObject.Kind == Git.Commit.Object.Type.Blob;
                saveAs.Click += (obj, ev) => {
                    FolderDialog.Open(App.Text("SaveFileTo"), saveTo => {
                        var path = Path.Combine(saveTo, node.Name);
                        commit.SaveFileTo(repo, node.FilePath, path);
                    });
                };
                menu.Items.Add(saveAs);
            }

            MenuItem copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Click += (obj, ev) => {
                Clipboard.SetText(node.FilePath);
            };
            menu.Items.Add(copyPath);
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void TreeRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }
        #endregion
    }
}
