using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

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
            public bool IsNodeExpanded { get; set; } = true;
            public Git.Change Change { get; set; } = null;
            public List<Node> Children { get; set; } = new List<Node>();
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
        ///     Has conflict object?
        /// </summary>
        private bool hasConflict = false;

        /// <summary>
        ///     Constructor.
        /// </summary>
        public WorkingCopy() {
            InitializeComponent();
        }

        /// <summary>
        ///     Set display data.
        /// </summary>
        /// <param name="changes"></param>
        public bool SetData(List<Git.Change> changes) {
            List<Git.Change> staged = new List<Git.Change>();
            List<Git.Change> unstaged = new List<Git.Change>();
            hasConflict = false;

            foreach (var c in changes) {
                hasConflict = hasConflict || c.IsConflit;

                if (c.Index != Git.Change.Status.None && c.Index != Git.Change.Status.Untracked) {
                    staged.Add(c);
                }

                if (c.WorkTree != Git.Change.Status.None) {
                    unstaged.Add(c);
                }
            }

            Dispatcher.Invoke(() => mergePanel.Visibility = Visibility.Collapsed);

            SetData(unstaged, true);
            SetData(staged, false);

            Dispatcher.Invoke(() => {
                var current = Repo.CurrentBranch();
                if (current != null && !string.IsNullOrEmpty(current.Upstream)) {
                    btnCommitAndPush.Visibility = Visibility.Visible;
                } else {
                    btnCommitAndPush.Visibility = Visibility.Collapsed;
                }

                diffViewer.Reset();
            });

            return hasConflict;
        }

        /// <summary>
        ///     Try to load merge message.
        /// </summary>
        public void LoadMergeMessage() {            
            if (string.IsNullOrEmpty(txtCommitMsg.Text)) {
                var mergeMsgFile = Path.Combine(Repo.Path, ".git", "MERGE_MSG");
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

        #region UNSTAGED
        private void UnstagedTreeMultiSelectionChanged(object sender, RoutedEventArgs e) {
            mergePanel.Visibility = Visibility.Collapsed;
            diffViewer.Reset();

            var selected = Helpers.TreeViewHelper.GetSelectedItems(unstagedTree);
            if (selected.Count == 0) return;

            Helpers.TreeViewHelper.UnselectTree(stageTree);
            stageList.SelectedItems.Clear();

            if (selected.Count != 1) return;

            var node = selected[0].DataContext as Node;
            if (!node.IsFile) return;

            if (node.Change.IsConflit) {
                mergePanel.Visibility = Visibility.Visible;
                return;
            }

            List<string> data;
            switch (node.Change.WorkTree) {
            case Git.Change.Status.Added:
            case Git.Change.Status.Untracked:
                data = Repo.Diff("", "--no-index", node.FilePath, "/dev/null");
                break;
            default:
                data = Repo.Diff("", "", node.FilePath, node.Change.OriginalPath);
                break;
            }

            diffViewer.SetData(data, node.FilePath, node.Change.OriginalPath);
        }

        private void UnstagedListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selected = unstagedList.SelectedItems;
            if (selected.Count == 0) return;

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

            List<string> data;
            switch (change.WorkTree) {
            case Git.Change.Status.Added:
            case Git.Change.Status.Untracked:
                data = Repo.Diff("", "--no-index", change.Path, "/dev/null");
                break;
            default:
                data = Repo.Diff("", "", change.Path, change.OriginalPath);
                break;
            }

            diffViewer.SetData(data, change.Path, change.OriginalPath);
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
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, e) => {
                    if (node.IsFile) Process.Start("explorer", $"/select,{path}");
                    else Process.Start(path);
                    e.Handled = true;
                };

                var stage = new MenuItem();
                stage.Header = "Stage";
                stage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Stage(node.FilePath));
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = "Discard changes ...";
                discard.Click += (o, e) => {
                    Discard.Show(Repo, changes);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = $"Stash ...";
                stash.Click += (o, e) => {
                    List<string> nodes = new List<string>() { node.FilePath };
                    Stash.Show(Repo, nodes);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = $"Save as patch ...";
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = "Select file to store patch data.";
                    dialog.InitialDirectory = Repo.Path;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatchFromUnstagedChanges(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = "Copy full path";
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
                menu.Items.Add(copyPath);
                menu.IsOpen = true;
            } else if (selected.Count > 1) {
                var changes = new List<Git.Change>();
                var files = new List<string>();

                foreach (var item in selected) GetChangesFromNode(item.DataContext as Node, changes);
                foreach (var c in changes) files.Add(c.Path);                

                var stage = new MenuItem();
                stage.Header = $"Stage {changes.Count} files ...";
                stage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Stage(files.ToArray()));
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = $"Discard {changes.Count} changes ...";
                discard.Click += (o, e) => {
                    Discard.Show(Repo, changes);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = $"Stash {changes.Count} files ...";
                stash.Click += (o, e) => {
                    Stash.Show(Repo, files);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = $"Save as patch ...";
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = "Select file to store patch data.";
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
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, e) => {
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };

                var stage = new MenuItem();
                stage.Header = "Stage";
                stage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Stage(change.Path));
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = "Discard changes ...";
                discard.Click += (o, e) => {
                    Discard.Show(Repo, new List<Git.Change>() { change });
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = $"Stash ...";
                stash.Click += (o, e) => {
                    List<string> nodes = new List<string>() { change.Path };
                    Stash.Show(Repo, nodes);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = $"Save as patch ...";
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = "Select file to store patch data.";
                    dialog.InitialDirectory = Repo.Path;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatchFromUnstagedChanges(dialog.FileName, new List<Git.Change>() { change });
                    }

                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = "Copy file path";
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
                stage.Header = $"Stage {changes.Count} files ...";
                stage.Click += async (o, e) => {                    
                    await Task.Run(() => Repo.Stage(files.ToArray()));
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = $"Discard {changes.Count} changes ...";
                discard.Click += (o, e) => {
                    Discard.Show(Repo, changes);
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = $"Stash {changes.Count} files ...";
                stash.Click += (o, e) => {
                    Stash.Show(Repo, files);
                    e.Handled = true;
                };

                var patch = new MenuItem();
                patch.Header = $"Save as patch ...";
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = "Select file to store patch data.";
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

        private async void Stage(object sender, RoutedEventArgs e) {
            var files = new List<string>();

            if (App.Preference.UIUseListInUnstaged) {
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

            if (files.Count == 0) return;
            await Task.Run(() => Repo.Stage(files.ToArray()));
        }

        private async void StageAll(object sender, RoutedEventArgs e) {
            await Task.Run(() => Repo.Stage());
        }
        #endregion

        #region STAGED
        private void StageTreeMultiSelectionChanged(object sender, RoutedEventArgs e) {
            mergePanel.Visibility = Visibility.Collapsed;
            diffViewer.Reset();

            var selected = Helpers.TreeViewHelper.GetSelectedItems(stageTree);
            if (selected.Count == 0) return;
            
            Helpers.TreeViewHelper.UnselectTree(unstagedTree);
            unstagedList.SelectedItems.Clear();

            if (selected.Count != 1) return;

            var node = selected[0].DataContext as Node;
            if (!node.IsFile) return;

            mergePanel.Visibility = Visibility.Collapsed;
            List<string> data = Repo.Diff("", "--cached", node.FilePath, node.Change.OriginalPath);
            diffViewer.SetData(data, node.FilePath, node.Change.OriginalPath);
            e.Handled = true;
        }

        private void StagedListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selected = stageList.SelectedItems;
            if (selected.Count == 0) return;

            mergePanel.Visibility = Visibility.Collapsed;
            diffViewer.Reset();
            Helpers.TreeViewHelper.UnselectTree(unstagedTree);
            unstagedList.SelectedItems.Clear();

            if (selected.Count != 1) return;

            var change = selected[0] as Git.Change;
            mergePanel.Visibility = Visibility.Collapsed;
            List<string> data = Repo.Diff("", "--cached", change.Path, change.OriginalPath);
            diffViewer.SetData(data, change.Path, change.OriginalPath);
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
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, e) => {
                    if (node.IsFile) Process.Start("explorer", $"/select,{path}");
                    else Process.Start(path);
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = "Unstage";
                unstage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Unstage(node.FilePath));
                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = "Copy full path";
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
                unstage.Header = $"Unstage {changes.Count} files";
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
                explore.Header = "Reveal in File Explorer";
                explore.Click += (o, e) => {
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = "Unstage";
                unstage.Click += async (o, e) => {
                    await Task.Run(() => Repo.Unstage(change.Path));
                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = "Copy full path";
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
                unstage.Header = $"Unstage {selected.Count} files";
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

            if (App.Preference.UIUseListInUnstaged) {
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
        private void OpenCommitMessageSelector(object sender, RoutedEventArgs e) {
            var anchor = sender as Button;

            if (anchor.ContextMenu == null) {
                anchor.ContextMenu = new ContextMenu();
                anchor.ContextMenu.PlacementTarget = anchor;
                anchor.ContextMenu.Placement = PlacementMode.Top;
                anchor.ContextMenu.VerticalOffset = -4;
                anchor.ContextMenu.StaysOpen = false;
                anchor.ContextMenu.Focusable = true;
                anchor.ContextMenu.MaxWidth = 500;
            } else {
                anchor.ContextMenu.Items.Clear();
            }

            if (Repo.CommitMsgRecords.Count == 0) {
                var tip = new MenuItem();
                tip.Header = "NO RECENT INPUT MESSAGES";
                tip.IsEnabled = false;
                anchor.ContextMenu.Items.Add(tip);
            } else {
                var tip = new MenuItem();
                tip.Header = "RECENT INPUT MESSAGES";
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

        private void CommitMessageChanged(object sender, TextChangedEventArgs e) {
            (sender as TextBox).ScrollToEnd();
        }

        private void StartAmend(object sender, RoutedEventArgs e) {
            var commits = Repo.Commits("-n 1");
            if (commits.Count == 0) {
                App.RaiseError("No commit to amend!");
                chkAmend.IsChecked = false;
                return;
            }

            txtCommitMsg.Text = commits[0].Subject;
        }

        private async void Commit(object sender, RoutedEventArgs e) {
            var amend = chkAmend.IsChecked == true;

            Repo.RecordCommitMessage(CommitMessage);

            if (hasConflict) {
                App.RaiseError("You have unsolved conflicts in your working copy!");
                return;
            }

            if (stageTree.Items.Count == 0) {
                App.RaiseError("Nothing to commit!");
                return;
            }

            txtCommitMsg.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtCommitMsg)) return;

            bool succ = await Task.Run(() => Repo.DoCommit(CommitMessage, amend));
            if (succ) ClearMessage();
        }

        private async void CommitAndPush(object sender, RoutedEventArgs e) {
            var amend = chkAmend.IsChecked == true;

            Repo.RecordCommitMessage(CommitMessage);

            if (hasConflict) {
                App.RaiseError("You have unsolved conflicts in your working copy!");
                return;
            }

            if (stageTree.Items.Count == 0) {
                App.RaiseError("Nothing to commit!");
                return;
            }

            txtCommitMsg.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtCommitMsg)) return;

            bool succ = await Task.Run(() => Repo.DoCommit(CommitMessage, amend));
            if (!succ) return;

            ClearMessage();
            Push.StartDirectly(Repo);
        }
        #endregion

        #region MERGE
        private async void OpenMergeTool(object sender, RoutedEventArgs e) {
            var mergeExe = App.Preference.MergeExecutable;
            var mergeParam = Git.MergeTool.Supported[App.Preference.MergeTool].Parameter;

            if (!File.Exists(mergeExe) || mergeParam.IndexOf("$MERGED") < 0) {
                App.RaiseError("Invalid merge tool in preference setting!");
                return;
            }

            string file = null;
            if (App.Preference.UIUseListInUnstaged) {
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

            await Task.Run(() => {
                Repo.RunCommand($"-c mergetool.sourcegit.cmd=\"\\\"{mergeExe}\\\" {mergeParam}\" -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=sourcegit {file}", null);
            });
        }

        private async void UseTheirs(object sender, RoutedEventArgs e) {
            var files = new List<string>();
            if (App.Preference.UIUseListInUnstaged) {
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
            if (App.Preference.UIUseListInUnstaged) {
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

        private void SetData(List<Git.Change> changes, bool unstaged) {
            List<Node> source = new List<Node>();
            Dictionary<string, Node> folders = new Dictionary<string, Node>();
            bool isExpendDefault = changes.Count <= 50;

            foreach (var c in changes) {
                var subs = c.Path.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (subs.Length == 1) {
                    Node node = new Node();
                    node.FilePath = c.Path;
                    node.IsFile = true;
                    node.Name = c.Path;
                    node.Change = c;
                    source.Add(node);
                } else {
                    Node lastFolder = null;
                    var folder = "";
                    for (int i = 0; i < subs.Length - 1; i++) {
                        folder += (subs[i] + "/");
                        if (folders.ContainsKey(folder)) {
                            lastFolder = folders[folder];
                        } else if (lastFolder == null) {
                            lastFolder = new Node();
                            lastFolder.FilePath = folder;
                            lastFolder.Name = subs[i];
                            lastFolder.IsNodeExpanded = isExpendDefault;
                            source.Add(lastFolder);
                            folders.Add(folder, lastFolder);
                        } else {
                            var folderNode = new Node();
                            folderNode.FilePath = folder;
                            folderNode.Name = subs[i];
                            folderNode.IsNodeExpanded = isExpendDefault;
                            folders.Add(folder, folderNode);
                            lastFolder.Children.Add(folderNode);
                            lastFolder = folderNode;
                        }
                    }

                    Node node = new Node();
                    node.FilePath = c.Path;
                    node.Name = subs[subs.Length - 1];
                    node.IsFile = true;
                    node.Change = c;
                    lastFolder.Children.Add(node);
                }
            }

            folders.Clear();            
            SortTreeNodes(source);

            Dispatcher.Invoke(() => {                
                if (unstaged) {
                    unstagedList.ItemsSource = changes;
                    unstagedTree.ItemsSource = source;
                } else {
                    stageList.ItemsSource = changes;
                    stageTree.ItemsSource = source;
                }
            });
        }

        private Node FindNodeByPath(List<Node> nodes, string filePath) {
            foreach (var node in nodes) {
                if (node.FilePath == filePath) return node;
                var found = FindNodeByPath(node.Children, filePath);
                if (found != null) return found;
            }
            return null;
        }

        private void SortTreeNodes(List<Node> list) {
            list.Sort((l, r) => {
                if (l.IsFile) {
                    return r.IsFile ? l.FilePath.CompareTo(r.FilePath) : 1;
                } else {
                    return r.IsFile ? -1 : l.FilePath.CompareTo(r.FilePath);
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
