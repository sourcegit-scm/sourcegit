using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SourceGit.UI {

    /// <summary>
    ///     Interaction logic for FolderDailog.xaml
    /// </summary>
    public partial class FolderDailog : Window {
        private Action<string> cb = null;
        private Node root = new Node("", "");
        private Node selected = null;

        /// <summary>
        ///     Tree node.
        /// </summary>
        public class Node : INotifyPropertyChanged {
            private bool isOpened = false;

            /// <summary>
            ///     Display name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            ///     Full path in file-system.
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            ///     Is opened.
            /// </summary>
            public bool IsOpened {
                get { return isOpened; }
                set {
                    if (isOpened != value) {
                        isOpened = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsOpened"));
                    }
                }
            }

            /// <summary>
            ///     Children nodes.
            /// </summary>
            public ObservableCollection<Node> Children { get; set; }

            /// <summary>
            ///     INotifyPropertyChanged.PropertyChanged
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            ///     Constructor.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="path"></param>
            /// <param name="isOpen"></param>
            public Node(string name, string path) {
                Name = name;
                Path = path;
                isOpened = false;
                Children = new ObservableCollection<Node>();
            }

            /// <summary>
            ///     Collect children.
            /// </summary>
            public void CollectChildren() {
                Children.Clear();

                try {
                    var dir = new DirectoryInfo(Path);
                    var subs = dir.GetDirectories();

                    foreach (var sub in subs) {
                        if ((sub.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                        Children.Add(new Node(sub.Name, sub.FullName));
                    }
                } catch {}

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Children"));
            }
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="title"></param>
        /// <param name="initPath"></param>
        public FolderDailog(string title, string initPath) {
            InitializeComponent();

            // Move to center.
            var parent = App.Current.MainWindow;
            Left = parent.Left + (parent.Width - Width) * 0.5;
            Top = parent.Top + (parent.Height - Height) * 0.5;

            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives) {
                var node = new Node(drive.Name, drive.Name);

                node.CollectChildren();
                if (initPath != null && initPath.StartsWith(drive.Name)) InitializePath(node, initPath);

                root.Children.Add(node);
            }

            txtTitle.Content = title.ToUpper();
            treePath.ItemsSource = root.Children;

            if (selected != null) {
                Helpers.TreeViewHelper.SelectOneByContext(treePath, selected);
            } else {
                btnSure.IsEnabled = false;
            }
        }

        /// <summary>
        ///     Set callbacks on click OK.
        /// </summary>
        /// <param name="onOK"></param>
        public void Open(Action<string> onOK) {
            cb = onOK;
            Show();
        }

        /// <summary>
        ///     Initialize given path.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="initPath"></param>
        private void InitializePath(Node parent, string initPath) {
            foreach (var child in parent.Children) {
                child.CollectChildren();
                if (child.Path == initPath) {
                    selected = child;
                } else if (initPath.StartsWith(child.Path)) {
                    InitializePath(child, initPath);
                }
            }
        }

        #region EVENTS
        private void OnSure(object sender, RoutedEventArgs e) {
            if (selected != null) cb?.Invoke(selected.Path);
            Close();
        }

        private void OnQuit(object sender, RoutedEventArgs e) {
            Close();
        }

        private void OnTreeMouseWheel(object sender, MouseWheelEventArgs e) {
            var scroll = Helpers.TreeViewHelper.GetScrollViewer(sender as TreeView);
            if (scroll == null) return;

            if (e.Delta > 0) {
                scroll.LineUp();
            } else {
                scroll.LineDown();
            }

            e.Handled = true;
        }

        private void OnTreeSelectedChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            selected = treePath.SelectedItem as Node;

            if (selected != null) {
                btnSure.IsEnabled = true;
                txtSelected.Content = selected.Path;
            } else {
                btnSure.IsEnabled = false;
                txtSelected.Content = "NONE";
            }

            e.Handled = true;
        }

        private void OnTreeNodeExpanded(object sender, RoutedEventArgs e) {
            var item = sender as TreeViewItem;
            if (item == null) return;

            var node = item.DataContext as Node;
            if (node == null) return;

            node.IsOpened = true;
            foreach (var c in node.Children) {
                c.CollectChildren();
            }

            e.Handled = true;
        }

        private void OnTreeNodeCollapsed(object sender, RoutedEventArgs e) {
            var item = sender as TreeViewItem;
            if (item == null) return;

            var node = item.DataContext as Node;
            if (node == null) return;

            node.IsOpened = false;
            e.Handled = true;
        }
        #endregion
    }
}
