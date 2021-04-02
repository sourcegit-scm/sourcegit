using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Working copy panel.
    /// </summary>
    public partial class WorkingCopy : UserControl {

        /// <summary>
        ///     Node for file tree.
        /// </summary>
        public class Node {
            public string FilePath { get; set; } = "";
            public string Name { get; set; } = "";
            public bool IsFile { get; set; } = false;
            public bool IsNodeExpanded { get; set; } = false;
            public Git.Change Change { get; set; } = null;
            public ObservableCollection<Node> Children { get; set; } = new ObservableCollection<Node>();
        }

        /// <summary>
        ///     Current opened repository.
        /// </summary>
        public Git.Repository Repo { get; set; }

        /// <summary>
        ///     Just for Validation.
        /// </summary>
        public string CommitMessage { get; set; }

        /// <summary>
        ///     Cached unstaged changes in list/grid view.
        /// </summary>
        public ObservableCollection<Git.Change> UnstagedListData { get; set; }

        /// <summary>
        ///     Cached unstaged changes in TreeView.
        /// </summary>
        public ObservableCollection<Node> UnstagedTreeData { get; set; }

        /// <summary>
        ///     Cached staged changes in list/grid view.
        /// </summary>
        public ObservableCollection<Git.Change> StagedListData { get; set; }

        /// <summary>
        ///     Cached staged changes in TreeView.
        /// </summary>
        public ObservableCollection<Node> StagedTreeData { get; set; }

        /// <summary>
        ///     Last view change
        /// </summary>
        public Git.Change LastViewChange { get; set; }

        /// <summary>
        ///     Constructor.
        /// </summary>
        public WorkingCopy() {
            UnstagedListData = new ObservableCollection<Git.Change>();
            UnstagedTreeData = new ObservableCollection<Node>();
            StagedListData = new ObservableCollection<Git.Change>();
            StagedTreeData = new ObservableCollection<Node>();

            InitializeComponent();
        }

        /// <summary>
        ///     Set display data.
        /// </summary>
        /// <param name="changes"></param>
        public bool SetData(List<Git.Change> changes) {
            List<Git.Change> staged = new List<Git.Change>();
            List<Git.Change> unstaged = new List<Git.Change>();
            bool hasConflict = false;
            bool removeLastViewChange = true;

            foreach (var c in changes) {
                hasConflict = hasConflict || c.IsConflit;

                if (c.IsAddedToIndex) {
                    staged.Add(c);
                    if (LastViewChange != null && LastViewChange.IsAddedToIndex && c.Path == LastViewChange.Path) {
                        LastViewChange = c;
                        removeLastViewChange = false;
                    }
                }

                if (c.WorkTree != Git.Change.Status.None) {
                    unstaged.Add(c);
                    if (LastViewChange != null && !LastViewChange.IsAddedToIndex && c.Path == LastViewChange.Path) {
                        LastViewChange = c;
                        removeLastViewChange = false;
                    }
                }
            }

            if (removeLastViewChange) LastViewChange = null;

            Dispatcher.Invoke(() => {
                UpdateData(unstaged, UnstagedListData, UnstagedTreeData);
                UpdateData(staged, StagedListData, StagedTreeData);

                // Force trigger UpdateLayout for DataGrid.
                unstagedList.Columns.Add(new DataGridTextColumn());
                unstagedList.Columns.RemoveAt(unstagedList.Columns.Count - 1);
                stageList.Columns.Add(new DataGridTextColumn());
                stageList.Columns.RemoveAt(stageList.Columns.Count - 1);

                var current = Repo.CurrentBranch();
                if (current != null && !string.IsNullOrEmpty(current.Upstream) && chkAmend.IsChecked != true) {
                    btnCommitAndPush.Visibility = Visibility.Visible;
                } else {
                    btnCommitAndPush.Visibility = Visibility.Collapsed;
                }

                if (LastViewChange != null) {
                    if (LastViewChange.IsConflit) {
                        mergePanel.Visibility = Visibility.Visible;
                        diffViewer.Reset();
                    } else {
                        mergePanel.Visibility = Visibility.Collapsed;
                        diffViewer.Reload();
                    }
                } else {
                    mergePanel.Visibility = Visibility.Collapsed;
                    diffViewer.Reset();
                }
            });

            return hasConflict;
        }

        /// <summary>
        ///     Update data.
        /// </summary>
        /// <param name="changes"></param>
        /// <param name="list"></param>
        /// <param name="tree"></param>
        public void UpdateData(List<Git.Change> changes, ObservableCollection<Git.Change> list, ObservableCollection<Node> tree) {
            for (int i = list.Count - 1; i >= 0; i--) {
                var exist = list[i];
                if (changes.FirstOrDefault(one => one.Path == exist.Path) != null) continue;

                list.RemoveAt(i);
                RemoveTreeNode(tree, exist);
            }

            var isDefaultExpand = changes.Count <= 50;

            foreach (var c in changes) {
                if (list.FirstOrDefault(one => one.Path == c.Path) != null) continue;

                bool added = false;
                for (int i = 0; i < list.Count; i++) {
                    if (c.Path.CompareTo(list[i].Path) < 0) {
                        list.Insert(i, c);
                        added = true;
                        break;
                    }
                }
                if (!added) list.Add(c);

                InsertTreeNode(tree, c, isDefaultExpand);
            }
        }

        /// <summary>
        ///     Try to load merge message.
        /// </summary>
        public void LoadMergeMessage() {            
            if (string.IsNullOrEmpty(txtCommitMsg.Text)) {
                var mergeMsgFile = Path.Combine(Repo.GitDir, "MERGE_MSG");
                if (!File.Exists(mergeMsgFile)) return;

                var content = File.ReadAllText(mergeMsgFile);
                txtCommitMsg.Text = content;
            }
        }

        /// <summary>
        ///     Clear message.
        /// </summary>
        public void ClearMessage() {
            txtCommitMsg.Text = "";
            Validation.ClearInvalid(txtCommitMsg.GetBindingExpression(TextBox.TextProperty));
        }

        /// <summary>
        ///     Cleanup
        /// </summary>
        public void Cleanup() {
            Repo = null;
            UnstagedListData.Clear();
            UnstagedTreeData.Clear();
            StagedListData.Clear();
            StagedTreeData.Clear();
            diffViewer.Reset();
        }

        #region UNSTAGED
        private void UnstagedTreeMultiSelectionChanged(object sender, RoutedEventArgs e) {
            var selected = Helpers.TreeViewHelper.GetSelectedItems(unstagedTree);
            if (selected.Count == 0) return;

            LastViewChange = null;
            mergePanel.Visibility = Visibility.Collapsed;
            diffViewer.Reset();
            Helpers.TreeViewHelper.UnselectTree(stageTree);
            stageList.SelectedItems.Clear();

            if (selected.Count != 1) return;

            var node = selected[0].DataContext as Node;
            if (!node.IsFile) return;

            if (node.Change.IsConflit) {
                mergePanel.Visibility = Visibility.Visible;
                return;
            }

            LastViewChange = node.Change;

            DiffViewer.Option opt;
            switch (node.Change.WorkTree) {
            case Git.Change.Status.Added:
            case Git.Change.Status.Untracked:
                opt = new DiffViewer.Option() { ExtraArgs = "--no-index", Path = node.FilePath, OrgPath = "/dev/null" };
                break;
            default:
                opt = new DiffViewer.Option() { Path = node.FilePath, OrgPath = node.Change.OriginalPath };
                break;
            }

            diffViewer.Diff(Repo, opt);
        }

        private void UnstagedListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selected = unstagedList.SelectedItems;
            if (selected.Count == 0) return;

            LastViewChange = null;
            mergePanel.Visibility = Visibility.Collapsed;
            diffViewer.Reset();
            Helpers.TreeViewHelper.UnselectTree(stageTree);
            stageList.SelectedItems.Clear();

            if (selected.Count != 1) return;

            var change = selected[0] as Git.Change;
            if (change.IsConflit) {
                mergePanel.Visibility = Visibility.Visible;
                return;
            }

            LastViewChange = change;

            DiffViewer.Option opt;
            switch (change.WorkTree) {
            case Git.Change.Status.Added:
            case Git.Change.Status.Untracked:
                opt = new DiffViewer.Option() { ExtraArgs = "--no-index", Path = change.Path, OrgPath = "/dev/null" };
                break;
            default:
                opt = new DiffViewer.Option() { Path = change.Path, OrgPath = change.OriginalPath };
                break;
            }

            diffViewer.Diff(Repo, opt);
        }

        private void SaveAsPatchFromUnstagedChanges(string path, List<Git.Change> changes) {
            FileStream stream = new FileStream(path, FileMode.Create);
            StreamWriter writer = new StreamWriter(stream);

            foreach (var change in changes) {
                if (change.WorkTree == Git.Change.Status.Added || change.WorkTree == Git.Change.Status.Untracked) {
                    Repo.RunCommand($"diff --no-index --no-ext-diff --find-renames -- /dev/null \"{change.Path}\"", line => {
                        writer.WriteLine(line);
                    });
                } else {
                    var orgFile = string.IsNullOrEmpty(change.OriginalPath) ? "" : $"\"{change.OriginalPath}\"";
                    Repo.RunCommand($"diff --binary --no-ext-diff --find-renames --full-index -- {orgFile} \"{change.Path}\"", line => {
                        writer.WriteLine(line);
                    });
                }
            }

            writer.Flush();
            stream.Flush();
            writer.Close();
            stream.Close();
        }

        private void GetChangesFromNode(Node node, List<Git.Change> outs) {
            if (node.Change != null) {
                if (!outs.Contains(node.Change)) outs.Add(node.Change);
            } else if (node.Children.Count > 0) {
                foreach (var sub in node.Children) GetChangesFromNode(sub, outs);
            }
        }

        private void UnstagedTreeContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var selected = Helpers.TreeViewHelper.GetSelectedItems(unstagedTree);

            if (selected.Count == 1) {
                var item = sender as TreeViewItem;
                var node = item.DataContext as Node;
                if (node == null) return;

                var changes = new List<Git.Change>();
                GetChangesFromNode(node, changes);

                var path = Path.GetFullPath(Repo.Path + "\\" + node.FilePath);
                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, e) => {
                    if (node.IsFile) Process.Start("explorer", $"/select,{path}");
                    else Process.Start("explorer", path);
                    e.Handled = true;
                };

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.Stage");
                stage.Click += (o, e) => {
                    DoStage(node.FilePath);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.Discard");
                discard.Click += (o, e) => {
                    Discard.Show(Repo, changes);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Click += (o, e) => {
                    List<string> nodes = new List<string>() { node.FilePath };
                    Stash.Show(Repo, nodes);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = Repo.Path;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatchFromUnstagedChanges(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetText(node.FilePath);
                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(explore);
                menu.Items.Add(new Separator());
                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new Separator());
                if (node.Change != null) {
                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Click += (o, e) => {
                        var viewer = new FileHistories(Repo, node.FilePath);
                        viewer.Show();
                        e.Handled = true;
                    };
                    menu.Items.Add(history);
                    menu.Items.Add(new Separator());
                }
                menu.Items.Add(copyPath);
                menu.IsOpen = true;
            } else if (selected.Count > 1) {
                var changes = new List<Git.Change>();
                var files = new List<string>();

                foreach (var item in selected) GetChangesFromNode(item.DataContext as Node, changes);
                foreach (var c in changes) files.Add(c.Path);                

                var stage = new MenuItem();
                stage.Header = App.Format("FileCM.StageMulti", changes.Count);
                stage.Click += (o, e) => {
                    DoStage(files.ToArray());
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Format("FileCM.DiscardMulti", changes.Count);
                discard.Click += (o, e) => {
                    Discard.Show(Repo, changes);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Format("FileCM.StashMulti", changes.Count);
                stash.Click += (o, e) => {
                    Stash.Show(Repo, files);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = Repo.Path;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatchFromUnstagedChanges(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.IsOpen = true;
            }
            
            ev.Handled = true;
        }

        private void UnstagedListContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var row = sender as DataGridRow;
            if (row == null) return;

            if (!row.IsSelected) {
                unstagedList.SelectedItems.Clear();
                unstagedList.SelectedItems.Add(row.DataContext);
            }

            var selected = unstagedList.SelectedItems;
            var brush = new SolidColorBrush(Color.FromRgb(48, 48, 48));

            if (selected.Count == 1) {
                var change = selected[0] as Git.Change;
                var path = Path.GetFullPath(Repo.Path + "\\" + change.Path);
                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, e) => {
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.Stage");
                stage.Click += (o, e) => {
                    DoStage(change.Path);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.Discard");
                discard.Click += (o, e) => {
                    Discard.Show(Repo, new List<Git.Change>() { change });
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Click += (o, e) => {
                    List<string> nodes = new List<string>() { change.Path };
                    Stash.Show(Repo, nodes);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = Repo.Path;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatchFromUnstagedChanges(dialog.FileName, new List<Git.Change>() { change });
                    }

                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetText(change.Path);
                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(explore);
                menu.Items.Add(new Separator());
                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new Separator());
                if (change != null) {
                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Click += (o, e) => {
                        var viewer = new FileHistories(Repo, change.Path);
                        viewer.Show();
                        e.Handled = true;
                    };
                    menu.Items.Add(history);
                    menu.Items.Add(new Separator());
                }
                menu.Items.Add(copyPath);
                menu.IsOpen = true;
            } else if (selected.Count > 1) {
                List<string> files = new List<string>();
                List<Git.Change> changes = new List<Git.Change>();
                foreach (var item in selected) {
                    files.Add((item as Git.Change).Path);
                    changes.Add(item as Git.Change);
                }

                var stage = new MenuItem();
                stage.Header = App.Format("FileCM.StageMulti", changes.Count);
                stage.Click += (o, e) => {
                    DoStage(files.ToArray());
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Format("FileCM.DiscardMulti", changes.Count);
                discard.Click += (o, e) => {
                    Discard.Show(Repo, changes);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Format("FileCM.StashMulti", changes.Count);
                stash.Click += (o, e) => {
                    Stash.Show(Repo, files);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = Repo.Path;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatchFromUnstagedChanges(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.IsOpen = true;
            }

            ev.Handled = true;
        }

        private void Stage(object sender, RoutedEventArgs e) {
            var files = new List<string>();

            if (App.Setting.UI.UnstageFileDisplayMode != Preference.FilesDisplayMode.Tree) {
                var selected = unstagedList.SelectedItems;
                foreach (var one in selected) {
                    var node = one as Git.Change;
                    if (node != null) files.Add(node.Path);
                }
            } else {
                var selected = Helpers.TreeViewHelper.GetSelectedItems(unstagedTree);
                foreach (var one in selected) {
                    var node = one.DataContext as Node;
                    if (node != null) files.Add(node.FilePath);
                }
            }

            if (files.Count > 0) DoStage(files.ToArray());
        }

        private void StageAll(object sender, RoutedEventArgs e) {
            DoStage();
        }

        private void DoStage(params string[] files) {
            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            iconStaging.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            iconStaging.Visibility = Visibility.Visible;
            Task.Run(() => {
                Repo.Stage(files);
                Dispatcher.Invoke(() => {
                    iconStaging.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                    iconStaging.Visibility = Visibility.Collapsed;
                });
            });
        }
        #endregion

        #region STAGED
        private void StageTreeMultiSelectionChanged(object sender, RoutedEventArgs e) {
            var selected = Helpers.TreeViewHelper.GetSelectedItems(stageTree);
            if (selected.Count == 0) return;

            LastViewChange = null;
            mergePanel.Visibility = Visibility.Collapsed;
            diffViewer.Reset();
            Helpers.TreeViewHelper.UnselectTree(unstagedTree);
            unstagedList.SelectedItems.Clear();

            if (selected.Count != 1) return;

            var node = selected[0].DataContext as Node;
            if (!node.IsFile) return;

            LastViewChange = node.Change;

            diffViewer.Diff(Repo, new DiffViewer.Option() {
                ExtraArgs = "--cached",
                Path = node.FilePath,
                OrgPath = node.Change.OriginalPath
            });
            e.Handled = true;
        }

        private void StagedListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selected = stageList.SelectedItems;
            if (selected.Count == 0) return;

            LastViewChange = null;
            mergePanel.Visibility = Visibility.Collapsed;
            diffViewer.Reset();
            Helpers.TreeViewHelper.UnselectTree(unstagedTree);
            unstagedList.SelectedItems.Clear();

            if (selected.Count != 1) return;

            var change = selected[0] as Git.Change;
            LastViewChange = change;
            diffViewer.Diff(Repo, new DiffViewer.Option() {
                ExtraArgs = "--cached",
                Path = change.Path,
                OrgPath = change.OriginalPath
            });
            e.Handled = true;
        }

        private void StageTreeContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var selected = Helpers.TreeViewHelper.GetSelectedItems(stageTree);
            var brush = new SolidColorBrush(Color.FromRgb(48, 48, 48));

            if (selected.Count == 1) {
                var item = sender as TreeViewItem;
                if (item == null) return;

                var node = item.DataContext as Node;
                if (node == null) return;

                var path = Path.GetFullPath(Repo.Path + "\\" + node.FilePath);

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, e) => {
                    if (node.IsFile) Process.Start("explorer", $"/select,{path}");
                    else Process.Start(path);
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Unstage(node.FilePath));
                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetText(node.FilePath);
                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(explore);
                menu.Items.Add(new Separator());
                menu.Items.Add(unstage);
                menu.Items.Add(new Separator());
                menu.Items.Add(copyPath);
                menu.IsOpen = true;
            } else if (selected.Count > 1) {
                var changes = new List<Git.Change>();
                var files = new List<string>();
                foreach (var item in selected) GetChangesFromNode(item.DataContext as Node, changes);
                foreach (var c in changes) files.Add(c.Path);

                var unstage = new MenuItem();
                unstage.Header = App.Format("FileCM.UnstageMulti", files.Count);
                unstage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Unstage(files.ToArray()));
                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(unstage);
                menu.IsOpen = true;
            }

            ev.Handled = true;
        }

        private void StagedListContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var row = sender as DataGridRow;
            if (row == null) return;

            if (!row.IsSelected) {
                stageList.SelectedItems.Clear();
                stageList.SelectedItems.Add(row.DataContext);
            }

            var selected = stageList.SelectedItems;
            var brush = new SolidColorBrush(Color.FromRgb(48, 48, 48));

            if (selected.Count == 1) {
                var change = selected[0] as Git.Change;
                var path = Path.GetFullPath(Repo.Path + "\\" + change.Path);

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, e) => {
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Unstage(change.Path));
                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetText(change.Path);
                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(explore);
                menu.Items.Add(new Separator());
                menu.Items.Add(unstage);
                menu.Items.Add(new Separator());
                menu.Items.Add(copyPath);
                menu.IsOpen = true;
            } else if (selected.Count > 1) {
                List<string> files = new List<string>();
                foreach (var one in selected) files.Add((one as Git.Change).Path);

                var unstage = new MenuItem();
                unstage.Header = App.Format("FileCM.UnstageMulti", files.Count);
                unstage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Unstage(files.ToArray()));
                    e.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(unstage);
                menu.IsOpen = true;
            }

            ev.Handled = true;
        }

        private async void Unstage(object sender, RoutedEventArgs e) {
            var files = new List<string>();

            if (App.Setting.UI.StagedFileDisplayMode != Preference.FilesDisplayMode.Tree) {
                var selected = stageList.SelectedItems;
                foreach (var one in selected) {
                    var node = one as Git.Change;
                    if (node != null) files.Add(node.Path);
                }
            } else {
                var selected = Helpers.TreeViewHelper.GetSelectedItems(stageTree);
                foreach (var one in selected) {
                    var node = one.DataContext as Node;
                    if (node != null) files.Add(node.FilePath);
                }
            }

            if (files.Count == 0) return;
            await Task.Run(() => Repo.Unstage(files.ToArray()));
        }

        private async void UnstageAll(object sender, RoutedEventArgs e) {
            await Task.Run(() => Repo.Unstage());
        }
        #endregion

        #region COMMIT_PANEL
        private void CommitMsgGotFocus(object sender, RoutedEventArgs e) {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (string.IsNullOrEmpty(textBox.Text) && !string.IsNullOrEmpty(Repo.CommitTemplate)) {
                textBox.Text = Repo.CommitTemplate;
            }
        }

        private void CommitMsgPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Delta > 0) {
                textBox.LineUp();
            } else {
                textBox.LineDown();
            }
        }

        private void OpenCommitMessageSelector(object sender, RoutedEventArgs e) {
            var anchor = sender as Button;

            if (anchor.ContextMenu == null) {
                anchor.ContextMenu = new ContextMenu();
                anchor.ContextMenu.PlacementTarget = anchor;
                anchor.ContextMenu.Placement = PlacementMode.Top;
                anchor.ContextMenu.VerticalOffset = 0;
                anchor.ContextMenu.StaysOpen = false;
                anchor.ContextMenu.Focusable = true;
                anchor.ContextMenu.MaxWidth = 500;
            } else {
                anchor.ContextMenu.Items.Clear();
            }

            if (Repo.CommitMsgRecords.Count == 0) {
                var tip = new MenuItem();
                tip.Header = App.Text("WorkingCopy.NoCommitHistories");
                tip.IsEnabled = false;
                anchor.ContextMenu.Items.Add(tip);
            } else {
                var tip = new MenuItem();
                tip.Header = App.Text("WorkingCopy.HasCommitHistories");
                tip.IsEnabled = false;
                anchor.ContextMenu.Items.Add(tip);
                anchor.ContextMenu.Items.Add(new Separator());

                foreach (var one in Repo.CommitMsgRecords) {
                    var dump = one;

                    var item = new MenuItem();
                    item.Header = dump;
                    item.Padding = new Thickness(0);
                    item.Click += (o, ev) => {
                        txtCommitMsg.Text = dump;
                        ev.Handled = true;
                    };

                    anchor.ContextMenu.Items.Add(item);
                }
            }           

            anchor.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void StartAmend(object sender, RoutedEventArgs e) {
            var commits = Repo.Commits("-n 1");
            if (commits.Count == 0) {
                App.RaiseError("No commit to amend!");
                chkAmend.IsChecked = false;
                return;
            }

            txtCommitMsg.Text = commits[0].Subject;
            btnCommitAndPush.Visibility = Visibility.Collapsed;
        }

        private void EndAmend(object sender, RoutedEventArgs e) {
            if (!IsLoaded) return;
            
            var current = Repo.CurrentBranch();
            if (current != null && !string.IsNullOrEmpty(current.Upstream)) {
                btnCommitAndPush.Visibility = Visibility.Visible;
            } else {
                btnCommitAndPush.Visibility = Visibility.Collapsed;
            }
        }

        private async void Commit(object sender, RoutedEventArgs e) {
            foreach (var c in UnstagedListData) {
                if (c.IsConflit) {
                    App.RaiseError("You have unsolved conflicts in your working copy!");
                    return;
                }
            }

            var amend = chkAmend.IsChecked == true;
            Repo.RecordCommitMessage(CommitMessage);

            if (stageTree.Items.Count == 0) {
                App.RaiseError("Nothing to commit!");
                return;
            }

            txtCommitMsg.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtCommitMsg)) return;

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            iconCommiting.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            iconCommiting.Visibility = Visibility.Visible;

            bool succ = await Task.Run(() => Repo.DoCommit(CommitMessage, amend));
            if (succ) Dispatcher.Invoke(ClearMessage);
            iconCommiting.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            iconCommiting.Visibility = Visibility.Collapsed;
        }

        private async void CommitAndPush(object sender, RoutedEventArgs e) {
            foreach (var c in UnstagedListData) {
                if (c.IsConflit) {
                    App.RaiseError("You have unsolved conflicts in your working copy!");
                    return;
                }   
            }

            var amend = chkAmend.IsChecked == true;
            Repo.RecordCommitMessage(CommitMessage);

            if (stageTree.Items.Count == 0) {
                App.RaiseError("Nothing to commit!");
                return;
            }

            txtCommitMsg.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtCommitMsg)) return;

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            iconCommiting.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            iconCommiting.Visibility = Visibility.Visible;
            bool succ = await Task.Run(() => Repo.DoCommit(CommitMessage, amend));
            iconCommiting.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            iconCommiting.Visibility = Visibility.Collapsed;
            if (!succ) return;

            ClearMessage();
            Push.StartDirectly(Repo);
        }
        #endregion

        #region MERGE
        private async void OpenMergeTool(object sender, RoutedEventArgs e) {
            var mergeExe = App.Setting.Tools.MergeExecutable;
            var mergeParam = Git.MergeTool.Supported[App.Setting.Tools.MergeTool].Parameter;

            if (!File.Exists(mergeExe) || mergeParam.IndexOf("$MERGED") < 0) {
                App.RaiseError("Invalid merge tool in preference setting!");
                return;
            }

            string file = null;
            if (App.Setting.UI.UnstageFileDisplayMode != Preference.FilesDisplayMode.Tree) {
                var selected = unstagedList.SelectedItems;
                if (selected.Count <= 0) return;

                var change = selected[0] as Git.Change;
                if (change == null) return;

                file = change.Path;
            } else {
                var selected = Helpers.TreeViewHelper.GetSelectedItems(unstagedTree);
                if (selected.Count <= 0) return;

                var node = selected[0].DataContext as Node;
                if (node == null || !node.IsFile) return;

                file = node.FilePath;
            }

            var cmd = $"-c mergetool.sourcegit.cmd=\"\\\"{mergeExe}\\\" {mergeParam}\" ";
            cmd += "-c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true ";
            cmd += $"mergetool --tool=sourcegit {file}";

            await Task.Run(() => Repo.RunCommand(cmd, null));
        }

        private async void UseTheirs(object sender, RoutedEventArgs e) {
            var files = new List<string>();
            if (App.Setting.UI.UnstageFileDisplayMode != Preference.FilesDisplayMode.Tree) {
                var selected = unstagedList.SelectedItems;
                foreach (var one in selected) {
                    var node = one as Git.Change;
                    if (node != null) files.Add(node.Path);
                }
            } else {
                var selected = Helpers.TreeViewHelper.GetSelectedItems(unstagedTree);
                foreach (var one in selected) {
                    var node = one.DataContext as Node;
                    if (node != null) files.Add(node.FilePath);
                }
            }

            await Task.Run(() => {
                Repo.SetWatcherEnabled(false);
                var errs = Repo.RunCommand($"checkout --theirs -- {string.Join(" ", files)}", null);
                if (errs != null) {
                    Repo.SetWatcherEnabled(true);
                    App.RaiseError("Use theirs failed: " + errs);
                    return;
                }

                Repo.Stage(files.ToArray());
            });
        }

        private async void UseMine(object sender, RoutedEventArgs e) {
            var files = new List<string>();
            if (App.Setting.UI.UnstageFileDisplayMode != Preference.FilesDisplayMode.Tree) {
                var selected = unstagedList.SelectedItems;
                foreach (var one in selected) {
                    var node = one as Git.Change;
                    if (node != null) files.Add(node.Path);
                }
            } else {
                var selected = Helpers.TreeViewHelper.GetSelectedItems(unstagedTree);
                foreach (var one in selected) {
                    var node = one.DataContext as Node;
                    if (node != null) files.Add(node.FilePath);
                }
            }

            await Task.Run(() => {
                Repo.SetWatcherEnabled(false);
                var errs = Repo.RunCommand($"checkout --ours -- {string.Join(" ", files)}", null);
                if (errs != null) {
                    Repo.SetWatcherEnabled(true);
                    App.RaiseError("Use mine failed: " + errs);
                    return;
                }

                Repo.Stage(files.ToArray());
            });
        }
        #endregion

        #region TREE_COMMON
        private void SelectWholeTree(object sender, ExecutedRoutedEventArgs e) {
            var tree = sender as TreeView;
            if (tree == null) return;

            Helpers.TreeViewHelper.SelectWholeTree(tree);
        }

        private Node InsertTreeNode(ObservableCollection<Node> nodes, string name, string path, bool isFile, Git.Change change, bool expand) {
            Node node = new Node();
            node.Name = name;
            node.FilePath = path;
            node.IsFile = isFile;
            node.Change = change;
            node.IsNodeExpanded = expand;

            bool isAdded = false;
            if (node.IsFile) {
                for (var i = 0; i < nodes.Count; i++) {
                    var cur = nodes[i];
                    if (!cur.IsFile) continue;
                    if (node.FilePath.CompareTo(cur.FilePath) > 0) continue;
                    nodes.Insert(i, node);
                    isAdded = true;
                    break;
                }
            } else {
                for (var i = 0; i < nodes.Count; i++) {
                    var cur = nodes[i];
                    if (cur.IsFile || node.FilePath.CompareTo(cur.FilePath) < 0) {
                        nodes.Insert(i, node);
                        isAdded = true;
                        break;
                    }
                }
            }

            if (!isAdded) nodes.Add(node);
            return node;
        }

        private void InsertTreeNode(ObservableCollection<Node> nodes, Git.Change change, bool expand) {
            string[] subs = change.Path.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (subs.Length == 1) {
                InsertTreeNode(nodes, change.Path, change.Path, true, change, false);
            } else {
                Node last = nodes.FirstOrDefault(o => o.Name == subs[0]);
                if (last == null) last = InsertTreeNode(nodes, subs[0], subs[0], false, null, expand);

                for (int i = 1; i < subs.Length - 1; i++) {
                    var p = last.Children.FirstOrDefault(o => o.Name == subs[i]);
                    if (p == null) p = InsertTreeNode(last.Children, subs[i], last.FilePath + "/" + subs[i], false, null, expand);
                    last = p;
                }

                InsertTreeNode(last.Children, subs[subs.Length - 1], change.Path, true, change, false);
            }
        }

        private bool RemoveTreeNode(ObservableCollection<Node> nodes, Git.Change change) {
            for (int i = nodes.Count - 1; i >= 0; i--) {
                if (nodes[i].FilePath == change.Path) {
                    nodes.RemoveAt(i);
                    return true;
                }

                if (nodes[i].IsFile) continue;

                if (RemoveTreeNode(nodes[i].Children, change)) {
                    if (nodes[i].Children.Count == 0) nodes.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private Node FindNodeByPath(ObservableCollection<Node> nodes, string filePath) {
            foreach (var node in nodes) {
                if (node.FilePath == filePath) return node;
                var found = FindNodeByPath(node.Children, filePath);
                if (found != null) return found;
            }
            return null;
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
        #endregion

        #region DATAGRID_COMMON
        private void SelectWholeDataGrid(object sender, ExecutedRoutedEventArgs e) {
            var grid = sender as DataGrid;
            if (grid == null) return;

            var source = grid.ItemsSource;
            foreach (var item in source) grid.SelectedItems.Add(item);
        }
        #endregion
    }
}
