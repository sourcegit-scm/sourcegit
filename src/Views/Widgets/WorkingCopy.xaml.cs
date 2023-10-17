using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     工作区
    /// </summary>
    public partial class WorkingCopy : UserControl {
        private Models.Repository repo = null;
        private bool isLFSEnabled = false;

        public string CommitMessage { get; set; }

        public WorkingCopy(Models.Repository repo) {
            this.repo = repo;
            this.isLFSEnabled = new Commands.LFS(repo.Path).IsEnabled();

            InitializeComponent();

            unstagedContainer.SetRepository(repo.Path);
            stagedContainer.SetRepository(repo.Path);
        }

        public void SetData(List<Models.Change> changes) {
            List<Models.Change> unstagedChanges = new List<Models.Change>();
            List<Models.Change> stagedChanges = new List<Models.Change>();

            foreach (var c in changes) {
                if (c.Index == Models.Change.Status.Modified
                    || c.Index == Models.Change.Status.Added
                    || c.Index == Models.Change.Status.Deleted
                    || c.Index == Models.Change.Status.Renamed) {
                    stagedChanges.Add(c);
                }

                if (c.WorkTree != Models.Change.Status.None) {
                    unstagedChanges.Add(c);
                }
            }

            Dispatcher.Invoke(() => {
                unstagedContainer.SetData(unstagedChanges);
                stagedContainer.SetData(stagedChanges);

                var current = repo.Branches.Find(x => x.IsCurrent);
                if (current != null && !string.IsNullOrEmpty(current.Upstream) && chkAmend.IsChecked != true) {
                    btnCommitAndPush.Visibility = Visibility.Visible;
                } else {
                    btnCommitAndPush.Visibility = Visibility.Collapsed;
                }

                mergePanel.Visibility = Visibility.Collapsed;

                var diffTarget = unstagedContainer.DiffTarget;
                if (diffTarget != null) {
                    if (diffTarget.IsConflit) {
                        mergePanel.Visibility = Visibility.Visible;
                        diffViewer.Reset();
                    } else {
                        diffViewer.Reload();
                    }

                    return;
                }

                diffTarget = stagedContainer.DiffTarget;
                if (diffTarget == null) {
                    diffViewer.Reset();
                } else {
                    diffViewer.Reload();
                }
            });
        }

        public void TryLoadMergeMessage() {
            if (string.IsNullOrEmpty(txtCommitMessage.Text)) {
                var mergeMsgFile = Path.Combine(repo.GitDir, "MERGE_MSG");
                if (!File.Exists(mergeMsgFile)) return;

                var content = File.ReadAllText(mergeMsgFile);
                txtCommitMessage.Text = content;
            }
        }

        public void ClearMessage() {
            txtCommitMessage.Text = "";
            Validation.ClearInvalid(txtCommitMessage.GetBindingExpression(TextBox.TextProperty));
        }

        public void ToggleIncludeUntracked(object sender, RoutedEventArgs e) {
            var watcher = Models.Watcher.Get(repo.Path);
            if (watcher != null) watcher.RefreshWC();
        }

        public void Discard(List<Models.Change> changes) {
            if (changes.Count >= unstagedContainer.Changes.Count && stagedContainer.Changes.Count == 0) {
                new Popups.Discard(repo.Path, null).Show();
            } else {
                new Popups.Discard(repo.Path, changes).Show();
            }
        }

        #region STAGE_UNSTAGE
        private void ViewAssumeUnchanged(object sender, RoutedEventArgs e) {
            var dialog = new AssumeUnchanged(repo.Path);
            dialog.Owner = App.Current.MainWindow;
            dialog.ShowDialog();
        }

        private void StageSelected(object sender, RoutedEventArgs e) {
            unstagedContainer.StageSelected();
        }

        private void StageAll(object sender, RoutedEventArgs e) {
            unstagedContainer.StageAll();
        }

        private void UnstageSelected(object sender, RoutedEventArgs e) {
            stagedContainer.UnstageSelected();
        }

        private void UnstageAll(object sender, RoutedEventArgs e) {
            stagedContainer.UnstageAll();
        }

        private void OnDiffTargetChanged(object sender, WorkingCopyChanges.DiffTargetChangedEventArgs e) {
            var container = sender as WorkingCopyChanges;
            if (container == null) return;

            if (e.Target == null) {
                if (e.HasOthers) {
                    if (container.IsUnstaged) {
                        stagedContainer.UnselectAll();
                    } else {
                        unstagedContainer.UnselectAll();
                    }

                    mergePanel.Visibility = Visibility.Collapsed;
                    diffViewer.Reset();
                }

                return;
            }

            if (container.IsUnstaged) {
                stagedContainer.UnselectAll();
            } else {
                unstagedContainer.UnselectAll();
            }

            var change = e.Target;
            if (change.IsConflit) {
                mergePanel.Visibility = Visibility.Visible;
                diffViewer.Reset();
                return;
            }

            mergePanel.Visibility = Visibility.Collapsed;
            if (container.IsUnstaged) {
                switch (change.WorkTree) {
                case Models.Change.Status.Added:
                case Models.Change.Status.Untracked:
                    diffViewer.Diff(repo.Path, new DiffViewer.Option() {
                        ExtraArgs = "--no-index",
                        Path = change.Path,
                        OrgPath = "/dev/null",
                        UseLFS = isLFSEnabled,
                        WCChanges = true,
                    });
                    break;
                default:
                    diffViewer.Diff(repo.Path, new DiffViewer.Option() {
                        Path = change.Path,
                        OrgPath = change.OriginalPath,
                        UseLFS = isLFSEnabled,
                        WCChanges = true,
                    });
                    break;
                }
            } else {
                diffViewer.Diff(repo.Path, new DiffViewer.Option() {
                    ExtraArgs = "--cached",
                    Path = change.Path,
                    OrgPath = change.OriginalPath,
                    UseLFS = isLFSEnabled
                });
            }
        }
        #endregion

        #region MERGE
        private async void UseTheirs(object sender, RoutedEventArgs e) {
            var change = unstagedContainer.DiffTarget;
            if (change == null || !change.IsConflit) return;

            Models.Watcher.SetEnabled(repo.Path, false);
            var succ = await Task.Run(() => new Commands.Checkout(repo.Path).File(change.Path, true));
            if (succ) {
                await Task.Run(() => new Commands.Add(repo.Path, new List<string>() { change.Path }).Exec());
            }
            Models.Watcher.SetEnabled(repo.Path, true);

            e.Handled = true;
        }

        private async void UseMine(object sender, RoutedEventArgs e) {
            var change = unstagedContainer.DiffTarget;
            if (change == null || !change.IsConflit) return;

            Models.Watcher.SetEnabled(repo.Path, false);
            var succ = await Task.Run(() => new Commands.Checkout(repo.Path).File(change.Path, false));
            if (succ) {
                await Task.Run(() => new Commands.Add(repo.Path, new List<string>() { change.Path }).Exec());
            }
            Models.Watcher.SetEnabled(repo.Path, true);

            e.Handled = true;
        }

        private async void UseMergeTool(object sender, RoutedEventArgs e) {
            var mergeType = Models.Preference.Instance.MergeTool.Type;
            var mergeExe = Models.Preference.Instance.MergeTool.Path;

            var merger = Models.MergeTool.Supported.Find(x => x.Type == mergeType);
            if (merger == null || merger.Type == 0 || !File.Exists(mergeExe)) {
                App.Exception(repo.Path, "Invalid merge tool in preference setting!");
                return;
            }

            var change = unstagedContainer.DiffTarget;
            if (change == null || !change.IsConflit) return;

            var cmd = new Commands.Command();
            cmd.Cwd = repo.Path;
            cmd.DontRaiseError = true;
            cmd.Args = $"-c mergetool.sourcegit.cmd=\"\\\"{mergeExe}\\\" {merger.Cmd}\" ";
            cmd.Args += "-c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true ";
            cmd.Args += $"mergetool --tool=sourcegit {change.Path}";

            await Task.Run(() => cmd.Exec());
            e.Handled = true;
        }
        #endregion

        #region COMMIT
        private void OpenCommitMessageRecorder(object sender, RoutedEventArgs e) {
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

            if (repo.CommitMessages.Count == 0) {
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

                foreach (var one in repo.CommitMessages) {
                    var dump = one;

                    var item = new MenuItem();
                    item.Header = dump;
                    item.Padding = new Thickness(0);
                    item.Click += (o, ev) => {
                        txtCommitMessage.Text = dump;
                        ev.Handled = true;
                    };

                    anchor.ContextMenu.Items.Add(item);
                }
            }

            anchor.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void StartAmend(object sender, RoutedEventArgs e) {
            var commits = new Commands.Commits(repo.Path, "-n 1", false).Result();
            if (commits.Count == 0) {
                App.Exception(repo.Path, "No commits to amend!");
                chkAmend.IsChecked = false;
                return;
            }

            txtCommitMessage.Text = commits[0].Subject;
            btnCommitAndPush.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void EndAmend(object sender, RoutedEventArgs e) {
            if (!IsLoaded) return;

            var current = repo.Branches.Find(x => x.IsCurrent);
            if (current != null && !string.IsNullOrEmpty(current.Upstream)) {
                btnCommitAndPush.Visibility = Visibility.Visible;
            } else {
                btnCommitAndPush.Visibility = Visibility.Collapsed;
            }

            e.Handled = true;
        }

        private async void Commit(object sender, RoutedEventArgs e) {
            var changes = await Task.Run(() => new Commands.LocalChanges(repo.Path).Result());
            var conflict = changes.Find(x => x.IsConflit);
            if (conflict != null) {
                App.Exception(repo.Path, "You have unsolved conflicts in your working copy!");
                return;
            }

            if (stagedContainer.Changes.Count == 0) {
                App.Exception(repo.Path, "No files added to commit!");
                return;
            }

            txtCommitMessage.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtCommitMessage)) return;

            repo.PushCommitMessage(CommitMessage);
            iconCommitting.Visibility = Visibility.Visible;
            iconCommitting.IsAnimating = true;

            Models.Watcher.SetEnabled(repo.Path, false);
            var amend = chkAmend.IsChecked == true;
            var succ = await Task.Run(() => new Commands.Commit(repo.Path, CommitMessage, amend).Exec());
            if (succ) {
                ClearMessage();
                if (amend) chkAmend.IsChecked = false;
            }

            iconCommitting.IsAnimating = false;
            iconCommitting.Visibility = Visibility.Collapsed;
            Models.Watcher.SetEnabled(repo.Path, true);

            e.Handled = true;
        }

        private async void CommitAndPush(object sender, RoutedEventArgs e) {
            var changes = await Task.Run(() => new Commands.LocalChanges(repo.Path).Result());
            var conflict = changes.Find(x => x.IsConflit);
            if (conflict != null) {
                App.Exception(repo.Path, "You have unsolved conflicts in your working copy!");
                return;
            }

            if (stagedContainer.Changes.Count == 0) {
                App.Exception(repo.Path, "No files added to commit!");
                return;
            }

            txtCommitMessage.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtCommitMessage)) return;

            repo.PushCommitMessage(CommitMessage);
            iconCommitting.Visibility = Visibility.Visible;
            iconCommitting.IsAnimating = true;

            Models.Watcher.SetEnabled(repo.Path, false);
            var succ = await Task.Run(() => new Commands.Commit(repo.Path, CommitMessage, false).Exec());
            if (succ) {
                new Popups.Push(repo, repo.Branches.Find(x => x.IsCurrent)).ShowAndStart();
                ClearMessage();
            }
            iconCommitting.IsAnimating = false;
            iconCommitting.Visibility = Visibility.Collapsed;
            Models.Watcher.SetEnabled(repo.Path, true);

            e.Handled = true;
        }
        
        private void CommitMessageKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                Commit(sender, e);
            }
        }
        #endregion
    }
}
