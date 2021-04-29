using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace SourceGit.Views {

    /// <summary>
    ///     目录选择对话框
    /// </summary>
    public partial class FolderBrowser : Window {

        /// <summary>
        ///     目录树节点.
        /// </summary>
        public class Node : INotifyPropertyChanged {
            public string Name { get; set; }
            public string Path { get; set; }
            public ObservableCollection<Node> Children { get; set; }
            public event PropertyChangedEventHandler PropertyChanged;

            public Node(string name, string path) {
                Name = name;
                Path = path;
                Children = new ObservableCollection<Node>();
            }

            public void CollectChildren() {
                Children.Clear();

                try {
                    var dir = new DirectoryInfo(Path);
                    var subs = dir.GetDirectories();

                    foreach (var sub in subs) {
                        if ((sub.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                        Children.Add(new Node(sub.Name, sub.FullName));
                    }
                } catch { }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Children"));
            }
        }

        public string Description { get; set; }
        public ObservableCollection<Node> Nodes { get; set; }

        public FolderBrowser(string description, Action<string> onOK) {
            Description = description;
            Nodes = new ObservableCollection<Node>();

            var drives = DriveInfo.GetDrives();
            foreach (var d in drives) {
                var node = new Node(d.Name, d.Name);
                node.CollectChildren();
                Nodes.Add(node);
            }

            InitializeComponent();

            btnSure.Click += (o, e) => {
                if (tree.Selected.Count == 0) return;
                var node = tree.Selected[0] as Node;
                onOK?.Invoke(node.Path);
                Close();
            };
        }

        public static void Open(Window owner, string description, Action<string> onOK) {
            var dialog = new FolderBrowser(description, onOK);
            if (owner == null) dialog.Owner = Application.Current.MainWindow;
            else dialog.Owner = owner;
            dialog.ShowDialog();
        }

        private void OnTreeNodeExpanded(object sender, RoutedEventArgs e) {
            var item = sender as Controls.TreeItem;
            if (item == null) return;

            var node = item.DataContext as Node;
            if (node == null) return;

            foreach (var c in node.Children) c.CollectChildren();
            e.Handled = true;
        }

        private void OnTreeSelectionChanged(object sender, RoutedEventArgs e) {
            if (tree.Selected.Count == 0) {
                btnSure.IsEnabled = false;
                txtSelected.Text = "NONE";
            } else {
                btnSure.IsEnabled = true;
                txtSelected.Text = (tree.Selected[0] as Node).Path;
            }
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
