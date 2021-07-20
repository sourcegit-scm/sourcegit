using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     提交信息面板中的文件列表分页
    /// </summary>
    public partial class RevisionFiles : UserControl {
        private string repo = null;
        private string sha = null;
        private bool isLFSEnabled = false;

        /// <summary>
        ///     文件列表树节点
        /// </summary>
        public class FileNode {
            public Models.ObjectType Type { get; set; } = Models.ObjectType.None;
            public string Path { get; set; } = "";
            public string SHA { get; set; } = null;
            public bool IsFolder => Type == Models.ObjectType.None;
            public List<FileNode> Children { get; set; } = new List<FileNode>();
        }

        public RevisionFiles() {
            InitializeComponent();
        }

        public void SetData(string repo, string sha, Commands.Context cancelToken) {
            this.repo = repo;
            this.sha = sha;
            this.isLFSEnabled = new Commands.LFS(repo).IsEnabled();

            var cmd = new Commands.RevisionObjects(repo, sha) { Ctx = cancelToken };
            Task.Run(() => {
                var objects = cmd.Result();
                if (cmd.Ctx.IsCancelRequested) return;

                var nodes = new List<FileNode>();
                var folders = new Dictionary<string, FileNode>();

                foreach (var obj in objects) {
                    var sepIdx = obj.Path.IndexOf('/');
                    if (sepIdx == -1) {
                        nodes.Add(new FileNode() {
                            Type = obj.Type,
                            Path = obj.Path,
                            SHA = obj.SHA,
                        });
                    } else {
                        FileNode lastFolder = null;
                        var start = 0;

                        while (sepIdx != -1) {
                            var folder = obj.Path.Substring(0, sepIdx);
                            if (folders.ContainsKey(folder)) {
                                lastFolder = folders[folder];
                            } else if (lastFolder == null) {
                                lastFolder = new FileNode() {
                                    Type = Models.ObjectType.None,
                                    Path = folder,
                                    SHA = null,
                                };
                                nodes.Add(lastFolder);
                                folders.Add(folder, lastFolder);
                            } else {
                                var cur = new FileNode() {
                                    Type = Models.ObjectType.None,
                                    Path = folder,
                                    SHA = null,
                                };
                                folders.Add(folder, cur);
                                lastFolder.Children.Add(cur);
                                lastFolder = cur;
                            }

                            start = sepIdx + 1;
                            sepIdx = obj.Path.IndexOf('/', start);
                        }

                        lastFolder.Children.Add(new FileNode() {
                            Type = obj.Type,
                            Path = obj.Path,
                            SHA = obj.SHA,
                        });
                    }

                    obj.Path = null;
                }

                folders.Clear();
                objects.Clear();

                SortFileNodes(nodes);

                Dispatcher.Invoke(() => {
                    treeFiles.ItemsSource = nodes;
                    GC.Collect();
                });
            });
        }

        public void Cleanup() {
            treeFiles.ItemsSource = new List<FileNode>();
        }

        private void SortFileNodes(List<FileNode> nodes) {
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

        private bool IsImageFile(string path) {
            return path.EndsWith(".png") ||
                path.EndsWith(".jpg") ||
                path.EndsWith(".jpeg") ||
                path.EndsWith(".ico") ||
                path.EndsWith(".bmp") ||
                path.EndsWith(".tiff") ||
                path.EndsWith(".gif");
        }

        #region EVENTS
        private void LayoutTextPreview(List<Models.TextLine> lines) {
            var font = new FontFamily("Consolas");

            var maxLineNumber = $"{lines.Count + 1}";
            var formatted = new FormattedText(
                maxLineNumber,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                12.0,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var offset = formatted.Width + 16;
            if (lines.Count * 16 > layerTextPreview.ActualHeight) offset += 8;

            txtPreviewData.ItemsSource = lines;
            txtPreviewData.Columns[0].Width = new DataGridLength(formatted.Width + 16, DataGridLengthUnitType.Pixel);
            txtPreviewData.Columns[1].Width = DataGridLength.Auto;
            txtPreviewData.Columns[1].Width = DataGridLength.SizeToCells;
            txtPreviewData.Columns[1].MinWidth = layerTextPreview.ActualWidth - offset;

            txtPreviewSplitter.Margin = new Thickness(formatted.Width + 15, 0, 0, 0);
        }

        private void OnTextPreviewSizeChanged(object sender, SizeChangedEventArgs e) {
            if (txtPreviewData == null) return;

            var offset = txtPreviewData.NonFrozenColumnsViewportHorizontalOffset;
            if (txtPreviewData.Items.Count * 16 > layerTextPreview.ActualHeight) offset += 8;

            txtPreviewData.Columns[1].Width = DataGridLength.Auto;
            txtPreviewData.Columns[1].Width = DataGridLength.SizeToCells;
            txtPreviewData.Columns[1].MinWidth = layerTextPreview.ActualWidth - offset;
            txtPreviewData.UpdateLayout();
        }

        private void OnTextPreviewContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var grid = sender as DataGrid;
            if (grid == null) return;

            var menu = new ContextMenu();

            var copyIcon = new System.Windows.Shapes.Path();
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
                    var line = item as Models.TextLine;
                    if (line == null) continue;

                    builder.Append(line.Data);
                    builder.AppendLine();
                }

                Clipboard.SetText(builder.ToString());
            };
            menu.Items.Add(copy);
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OnFilesSelectionChanged(object sender, RoutedEventArgs e) {
            layerTextPreview.Visibility = Visibility.Collapsed;
            layerImagePreview.Visibility = Visibility.Collapsed;
            layerRevisionPreview.Visibility = Visibility.Collapsed;
            layerBinaryPreview.Visibility = Visibility.Collapsed;
            txtPreviewData.ItemsSource = null;

            if (treeFiles.Selected.Count == 0) return;

            var node = treeFiles.Selected[0] as FileNode;
            switch (node.Type) {
            case Models.ObjectType.Blob:
                if (IsImageFile(node.Path)) {
                    var tmp = Path.GetTempFileName();
                    new Commands.SaveRevisionFile(repo, node.Path, sha, tmp).Exec();

                    layerImagePreview.Visibility = Visibility.Visible;
                    imgPreviewData.Source = new BitmapImage(new Uri(tmp, UriKind.Absolute));
                } else if (isLFSEnabled && new Commands.LFS(repo).IsFiltered(node.Path)) {
                    var lfs = new Commands.QueryLFSObject(repo, sha, node.Path).Result();
                    layerRevisionPreview.Visibility = Visibility.Visible;
                    iconRevisionPreview.Data = FindResource("Icon.LFS") as Geometry;
                    txtRevisionPreview.Text = "LFS SIZE: " + App.Text("Bytes", lfs.Size);
                } else if (new Commands.IsBinaryFile(repo, sha, node.Path).Result()) {
                    layerBinaryPreview.Visibility = Visibility.Visible;
                } else {
                    layerTextPreview.Visibility = Visibility.Visible;
                    Task.Run(() => {
                        var lines = new Commands.QueryFileContent(repo, sha, node.Path).Result();
                        Dispatcher.Invoke(() => LayoutTextPreview(lines));
                    });
                }
                break;
            case Models.ObjectType.Tag:
                layerRevisionPreview.Visibility = Visibility.Visible;
                iconRevisionPreview.Data = FindResource("Icon.Tag") as Geometry;
                txtRevisionPreview.Text = "TAG: " + node.SHA;
                break;
            case Models.ObjectType.Commit:
                layerRevisionPreview.Visibility = Visibility.Visible;
                iconRevisionPreview.Data = FindResource("Icon.Submodule") as Geometry;
                txtRevisionPreview.Text = "SUBMODULE: " + node.SHA;
                break;
            case Models.ObjectType.Tree:
                layerRevisionPreview.Visibility = Visibility.Visible;
                iconRevisionPreview.Data = FindResource("Icon.Tree") as Geometry;
                txtRevisionPreview.Text = "TREE: " + node.SHA;
                break;
            default:
                return;
            }
        }

        private void OnFilesContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var item = treeFiles.FindItem(e.OriginalSource as DependencyObject);
            if (item == null) return;

            var node = item.DataContext as FileNode;
            if (node == null || node.IsFolder) return;

            var history = new MenuItem();
            history.Header = App.Text("FileHistory");
            history.Click += (o, ev) => {
                var viewer = new Views.Histories(repo, node.Path);
                viewer.Show();
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Click += (obj, ev) => {
                var viewer = new Blame(repo, node.Path, sha);
                viewer.Show();
                ev.Handled = true;
            };

            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Click += (o, ev) => {
                var full = Path.GetFullPath(repo + "\\" + node.Path);
                Process.Start("explorer", $"/select,{full}");
                ev.Handled = true;
            };

            var saveAs = new MenuItem();
            saveAs.Header = App.Text("SaveAs");
            saveAs.IsEnabled = node.Type == Models.ObjectType.Blob;
            saveAs.Click += (obj, ev) => {
                var dialog = new Controls.FolderDialog();
                if (dialog.ShowDialog() == true) {
                    var full = Path.Combine(dialog.SelectedPath, Path.GetFileName(node.Path));
                    new Commands.SaveRevisionFile(repo, node.Path, sha, full).Exec();
                }
                ev.Handled = true;
            };

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Click += (obj, ev) => {
                Clipboard.SetText(node.Path);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(history);
            menu.Items.Add(blame);
            menu.Items.Add(explore);
            menu.Items.Add(saveAs);
            menu.Items.Add(copyPath);

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }
        #endregion
    }
}
