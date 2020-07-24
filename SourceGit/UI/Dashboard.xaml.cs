using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SourceGit.UI {
    
    /// <summary>
    ///     Branch node in tree.
    /// </summary>
    public class BranchNode {
        public string Name { get; set; }
        public Git.Branch Branch { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsCurrent => Branch != null ? Branch.IsCurrent : false;
        public bool IsFiltered => Branch != null ? Branch.IsFiltered : false;
        public string Track => Branch != null ? Branch.UpstreamTrack : "";
        public Visibility FilterVisibility => Branch == null ? Visibility.Collapsed : Visibility.Visible;
        public Visibility TrackVisibility => (Branch != null && !Branch.IsSameWithUpstream) ? Visibility.Visible : Visibility.Collapsed;
        public List<BranchNode> Children { get; set; }
    }

    /// <summary>
    ///     Remote node in tree.
    /// </summary>
    public class RemoteNode {
        public string Name { get; set; }
        public bool IsExpanded { get; set; }
        public List<BranchNode> Children { get; set; }
    }

    /// <summary>
    ///     Dashboard for opened repository.
    /// </summary>
    public partial class Dashboard : UserControl {
        private Git.Repository repo = null;
        private List<BranchNode> cachedLocalBranches = new List<BranchNode>();
        private List<RemoteNode> cachedRemotes = new List<RemoteNode>();
        private string abortCommand = null;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="repo">Opened repository.</param>
        public Dashboard(Git.Repository opened) {            
            opened.OnWorkingCopyChanged = UpdateLocalChanges;
            opened.OnTagChanged = UpdateTags;
            opened.OnStashChanged = UpdateStashes;
            opened.OnBranchChanged = () => UpdateBranches(false);
            opened.OnCommitsChanged = UpdateHistories;
            opened.OnSubmoduleChanged = UpdateSubmodules;
            opened.OnNavigateCommit = commit => {
                Dispatcher.Invoke(() => {
                    workspace.SelectedItem = historiesSwitch;
                    histories.Navigate(commit);
                });
            };

            InitializeComponent();

            repo = opened;
            repoName.Content = repo.Name;
            histories.Repo = opened;
            commits.Repo = opened;

            if (repo.Parent != null) {
                btnParent.Visibility = Visibility.Visible;
                txtParent.Content = repo.Parent.Name;
            } else {
                btnParent.Visibility = Visibility.Collapsed;
            }

            UpdateBranches();
            UpdateHistories();
            UpdateLocalChanges();
            UpdateStashes();
            UpdateTags();
            UpdateSubmodules();
        }

        #region DATA_UPDATE
        private void UpdateHistories() {
            Dispatcher.Invoke(() => {
                histories.SetLoadingEnabled(true);
            });

            Task.Run(() => {
                var args = "-8000 ";
                if (repo.LogFilters.Count > 0) {
                    args = args + string.Join(" ", repo.LogFilters);
                } else {
                    args = args + "--branches --remotes --tags";
                }

                var commits = repo.Commits(args);
                histories.SetCommits(commits);
            });
        }

        private void UpdateLocalChanges() {
            Task.Run(() => {
                var changes = repo.LocalChanges();
                var conflicts = commits.SetData(changes);

                Dispatcher.Invoke(() => {
                    localChangesBadge.Visibility = changes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                    localChangesCount.Content = changes.Count;
                    btnContinue.Visibility = conflicts ? Visibility.Collapsed : Visibility.Visible;
                    DetectMergeState();
                });
            });
        }

        private void UpdateStashes() {
            Task.Run(() => {
                var data = repo.Stashes();
                Dispatcher.Invoke(() => {
                    stashBadge.Visibility = data.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    stashCount.Content = data.Count;
                    stashes.SetData(repo, data);
                });
            });
        }

        private void BackupBranchNodeExpandState(Dictionary<string, bool> states, List<BranchNode> nodes, string prefix) {
            foreach (var node in nodes) {
                var path = prefix + "/" + node.Name;
                states.Add(path, node.IsExpanded);
                BackupBranchNodeExpandState(states, node.Children, path);
            }
        }

        private void MakeBranchNode(Git.Branch branch, List<BranchNode> collection, Dictionary<string, BranchNode> folders, Dictionary<string, bool> expandStates, string prefix) {
            var subs = branch.Name.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (!branch.IsLocal) {
                if (subs.Length < 2) return;
                subs = subs.Skip(1).ToArray();
            }

            branch.IsFiltered = repo.LogFilters.Contains(branch.FullName);

            if (subs.Length == 1) {
                var node = new BranchNode() {
                    Name = subs[0],
                    Branch = branch,
                    Children = new List<BranchNode>(),
                };
                collection.Add(node);
            } else {
                BranchNode lastFolder = null;
                string path = prefix;
                for (int i = 0; i < subs.Length - 1; i++) {
                    path = path + "/" + subs[i];
                    if (folders.ContainsKey(path)) {
                        lastFolder = folders[path];
                    } else if (lastFolder == null) {
                        lastFolder = new BranchNode() {
                            Name = subs[i],
                            IsExpanded = expandStates.ContainsKey(path) ? expandStates[path] : false,
                            Children = new List<BranchNode>(),
                        };
                        collection.Add(lastFolder);
                        folders.Add(path, lastFolder);
                    } else {
                        var folder = new BranchNode() {
                            Name = subs[i],
                            IsExpanded = expandStates.ContainsKey(path) ? expandStates[path] : false,
                            Children = new List<BranchNode>(),
                        };
                        lastFolder.Children.Add(folder);
                        folders.Add(path, folder);
                        lastFolder = folder;
                    }
                }

                BranchNode node = new BranchNode();
                node.Name = subs[subs.Length - 1];
                node.Branch = branch;
                node.Children = new List<BranchNode>();
                lastFolder.Children.Add(node);
            }
        }

        private void SortBranchNodes(List<BranchNode> collection) {
            collection.Sort((l, r) => {
                if (l.Branch != null) {
                    return r.Branch != null ? l.Branch.Name.CompareTo(r.Branch.Name) : -1;
                } else {
                    return r.Branch == null ? l.Name.CompareTo(r.Name) : 1;
                }
            });

            foreach (var sub in collection) {
                if (sub.Children.Count > 0) SortBranchNodes(sub.Children);
            }
        }

        private void UpdateBranches(bool force = true) {
            Task.Run(() => {
                var branches = repo.Branches(force);
                var remotes = repo.Remotes(true);
                var localBranchNodes = new List<BranchNode>();
                var remoteNodes = new List<RemoteNode>();
                var remoteMap = new Dictionary<string, RemoteNode>();
                var folders = new Dictionary<string, BranchNode>();
                var states = new Dictionary<string, bool>();

                BackupBranchNodeExpandState(states, cachedLocalBranches, "locals");
                foreach (var r in cachedRemotes) {
                    var prefix = $"remotes/{r.Name}";
                    states.Add(prefix, r.IsExpanded);
                    BackupBranchNodeExpandState(states, r.Children, prefix);
                }

                foreach (var b in branches) {
                    if (b.IsLocal) {
                        MakeBranchNode(b, localBranchNodes, folders, states, "locals");            
                    } else if (!string.IsNullOrEmpty(b.Remote)) {
                        RemoteNode remote = null;

                        if (!remoteMap.ContainsKey(b.Remote)) {
                            var key = "remotes/" + b.Remote;
                            remote = new RemoteNode() {
                                Name = b.Remote,
                                IsExpanded = states.ContainsKey(key) ? states[key] : false,
                                Children = new List<BranchNode>(),
                            };
                            remoteNodes.Add(remote);
                            remoteMap.Add(b.Remote, remote);
                        } else {
                            remote = remoteMap[b.Remote];
                        }

                        MakeBranchNode(b, remote.Children, folders, states, "remotes");
                    }
                }

                foreach (var r in remotes) {
                    if (!remoteMap.ContainsKey(r.Name)) {
                        var remote = new RemoteNode() {
                            Name = r.Name,
                            IsExpanded = false,
                            Children = new List<BranchNode>(),
                        };
                        remoteNodes.Add(remote);
                    }
                }

                SortBranchNodes(localBranchNodes);
                foreach (var r in remoteNodes) SortBranchNodes(r.Children);

                cachedLocalBranches = localBranchNodes;
                cachedRemotes = remoteNodes;

                Dispatcher.Invoke(() => {
                    localBranchTree.ItemsSource = localBranchNodes;
                    remoteBranchTree.ItemsSource = remoteNodes;
                });
            });
        }

        private void UpdateTags() {
            Task.Run(() => {
                var tags = repo.Tags(true);
                foreach (var t in tags) t.IsFiltered = repo.LogFilters.Contains(t.Name);

                Dispatcher.Invoke(() => {
                    tagCount.Content = $"TAGS ({tags.Count})";
                    tagList.ItemsSource = tags;
                });
            });
        }

        private void UpdateSubmodules() {
            Task.Run(() => {
                var submodules = repo.Submodules();
                Dispatcher.Invoke(() => {
                    submoduleCount.Content = $"SUBMODULES ({submodules.Count})";
                    submoduleList.ItemsSource = submodules;
                });
            });
        }

        private void Cleanup(object sender, RoutedEventArgs e) {
            localBranchTree.ItemsSource = null;
            remoteBranchTree.ItemsSource = null;
            tagList.ItemsSource = null;
            cachedLocalBranches.Clear();
            cachedRemotes.Clear();
        }
        #endregion

        #region TOOLBAR
        private void Close(object sender, RoutedEventArgs e) {
            if (PopupManager.IsLocked()) return;
            PopupManager.Close();

            cachedLocalBranches.Clear();
            cachedRemotes.Clear();

            repo.Close();
        }

        private void GotoParent(object sender, RoutedEventArgs e) {
            if (repo.Parent == null) return;
            repo.Parent.Open();
            e.Handled = true;
        }

        private void OpenFetch(object sender, RoutedEventArgs e) {
            Fetch.Show(repo);
        }

        private void OpenPull(object sender, RoutedEventArgs e) {
            Pull.Show(repo);
        }

        private void OpenPush(object sender, RoutedEventArgs e) {
            Push.Show(repo);
        }

        private void OpenStash(object sender, RoutedEventArgs e) {
            Stash.Show(repo, new List<string>());
        }

        private void OpenApply(object sender, RoutedEventArgs e) {
            Apply.Show(repo);
        }

        private void OpenSearch(object sender, RoutedEventArgs e) {
            if (PopupManager.IsLocked()) return;

            workspace.SelectedItem = historiesSwitch;
            if (histories.searchBar.Margin.Top == 0) {
                histories.HideSearchBar();
            } else {
                histories.OpenSearchBar();
            }
        }

        private void OpenConfigure(object sender, RoutedEventArgs e) {
            Configure.Show(repo);
        }

        private void OpenExplorer(object sender, RoutedEventArgs e) {
            Process.Start(repo.Path);
        }

        private void OpenTerminal(object sender, RoutedEventArgs e) {
            var bash = Path.Combine(App.Preference.GitExecutable, "..", "bash.exe");
            if (!File.Exists(bash)) {
                App.RaiseError("Can NOT locate bash.exe. Make sure bash.exe exists under the same folder with git.exe");
                return;
            }

            var start = new ProcessStartInfo();
            start.WorkingDirectory = repo.Path;
            start.FileName = bash;
            Process.Start(start);
        }
        #endregion

        #region HOT_KEYS
        public void OpenSearchBar(object sender, ExecutedRoutedEventArgs e) {
            workspace.SelectedItem = historiesSwitch;
            histories.OpenSearchBar();
        }

        public void HideSearchBar(object sender, ExecutedRoutedEventArgs e) {
            if (histories.Visibility == Visibility.Visible) {
                histories.HideSearchBar();
            }            
        }
        #endregion

        #region MERGE_ABORTS
        public void DetectMergeState() {
            var cherryPickMerge = Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD");
            var rebaseMerge = Path.Combine(repo.GitDir, "REBASE_HEAD");
            var revertMerge = Path.Combine(repo.GitDir, "REVERT_HEAD");
            var otherMerge = Path.Combine(repo.GitDir, "MERGE_HEAD");

            if (File.Exists(cherryPickMerge)) {
                abortCommand = "cherry-pick";
                txtMergeProcessing.Content = "Cherry-Pick merge request detected! Press 'Abort' to restore original HEAD";
            } else if (File.Exists(rebaseMerge)) {
                abortCommand = "rebase";
                txtMergeProcessing.Content = "Rebase merge request detected! Press 'Abort' to restore original HEAD";
            } else if (File.Exists(revertMerge)) {
                abortCommand = "revert";
                txtMergeProcessing.Content = "Revert merge request detected! Press 'Abort' to restore original HEAD";
            } else if (File.Exists(otherMerge)) {
                abortCommand = "merge";
                txtMergeProcessing.Content = "Merge request detected! Press 'Abort' to restore original HEAD";
            } else {
                abortCommand = null;
            }

            if (abortCommand != null) {
                abortPanel.Visibility = Visibility.Visible;
                if (commits.Visibility == Visibility.Visible) {
                    btnResolve.Visibility = Visibility.Collapsed;
                } else {
                    btnResolve.Visibility = Visibility.Visible;
                }

                commits.LoadMergeMessage();
            } else {
                abortPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void Resolve(object sender, RoutedEventArgs e) {
            workspace.SelectedItem = workingCopySwitch;
        }

        private async void Continue(object sender, RoutedEventArgs e) {
            if (abortCommand == null) return;

            await Task.Run(() => {
                repo.SetWatcherEnabled(false);
                var errs = repo.RunCommand($"-c core.editor=true {abortCommand} --continue", null);
                repo.AssertCommand(errs);
            });

            commits.ClearMessage();
        }

        private async void Abort(object sender, RoutedEventArgs e) {
            if (abortCommand == null) return;

            await Task.Run(() => {
                repo.SetWatcherEnabled(false);
                var errs = repo.RunCommand($"{abortCommand} --abort", null);
                repo.AssertCommand(errs);
            });

            commits.ClearMessage();
        }
        #endregion

        #region WORKSPACE
        private void SwitchWorkingCopy(object sender, RoutedEventArgs e) {
            if (commits == null || histories == null || stashes == null) return;

            commits.Visibility = Visibility.Visible;
            histories.Visibility = Visibility.Collapsed;
            stashes.Visibility = Visibility.Collapsed;

            if (abortPanel.Visibility == Visibility.Visible) {
                btnResolve.Visibility = Visibility.Collapsed;
            }
        }

        private void SwitchHistories(object sender, RoutedEventArgs e) {
            if (commits == null || histories == null || stashes == null) return;

            commits.Visibility = Visibility.Collapsed;
            histories.Visibility = Visibility.Visible;
            stashes.Visibility = Visibility.Collapsed;

            if (abortPanel.Visibility == Visibility.Visible) {
                btnResolve.Visibility = Visibility.Visible;
            }
        }

        private void SwitchStashes(object sender, RoutedEventArgs e) {
            if (commits == null || histories == null || stashes == null) return;

            commits.Visibility = Visibility.Collapsed;
            histories.Visibility = Visibility.Collapsed;
            stashes.Visibility = Visibility.Visible;

            if (abortPanel.Visibility == Visibility.Visible) {
                btnResolve.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region LOCAL_BRANCHES
        private void OpenNewBranch(object sender, RoutedEventArgs e) {
            CreateBranch.Show(repo);
        }

        private void OpenGitFlow(object sender, RoutedEventArgs ev) {
            var button = sender as Button;
            if (button.ContextMenu == null) {
                button.ContextMenu = new ContextMenu();
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                button.ContextMenu.StaysOpen = false;
                button.ContextMenu.Focusable = true;
            } else {
                button.ContextMenu.Items.Clear();
            }

            if (repo.IsGitFlowEnabled()) {
                var startFeature = new MenuItem();
                startFeature.Header = "Start Feature ...";
                startFeature.Click += (o, e) => {
                    GitFlowStartBranch.Show(repo, Git.Branch.Type.Feature);
                    e.Handled = true;
                };

                var startRelease = new MenuItem();
                startRelease.Header = "Start Release ...";
                startRelease.Click += (o, e) => {
                    GitFlowStartBranch.Show(repo, Git.Branch.Type.Release);
                    e.Handled = true;
                };

                var startHotfix = new MenuItem();
                startHotfix.Header = "Start Hotfix ...";
                startHotfix.Click += (o, e) => {
                    GitFlowStartBranch.Show(repo, Git.Branch.Type.Hotfix);
                    e.Handled = true;
                };

                button.ContextMenu.Items.Add(startFeature);
                button.ContextMenu.Items.Add(startRelease);
                button.ContextMenu.Items.Add(startHotfix);
            } else {
                var init = new MenuItem();
                init.Header = "Initialize Git-Flow";
                init.Click += (o, e) => {
                    GitFlowSetup.Show(repo);
                    e.Handled = true;
                };
                button.ContextMenu.Items.Add(init);
            }

            button.ContextMenu.IsOpen = true;
            ev.Handled = true;
        }

        private void LocalBranchSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            var node = e.NewValue as BranchNode;
            if (node == null || node.Branch == null) return;
            repo.OnNavigateCommit?.Invoke(node.Branch.Head);
        }

        private void LocalBranchMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var node = (sender as TreeViewItem).DataContext as BranchNode;
            if (node == null || node.Branch == null) return;
            Task.Run(() => repo.Checkout(node.Branch.Name));
        }

        private void LocalBranchContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var node = (sender as TreeViewItem).DataContext as BranchNode;
            if (node == null || node.Branch == null) return;

            var menu = new ContextMenu();
            var branch = node.Branch;            

            var push = new MenuItem();
            push.Header = $"Push '{branch.Name}'";
            push.Click += (o, e) => {
                Push.Show(repo, branch);
                e.Handled = true;
            };

            if (branch.IsCurrent) {
                var discard = new MenuItem();
                discard.Header = "Discard all changes";
                discard.Click += (o, e) => {
                    Discard.Show(repo, null);
                    e.Handled = true;
                };
                menu.Items.Add(discard);
                menu.Items.Add(new Separator());

                if (!string.IsNullOrEmpty(branch.Upstream)) {
                    var upstream = branch.Upstream.Substring(13);
                    var fastForward = new MenuItem();
                    fastForward.Header = $"Fast-Forward to '{upstream}'";
                    fastForward.Click += (o, e) => {
                        Merge.StartDirectly(repo, upstream, branch.Name);
                        e.Handled = true;
                    };

                    var pull = new MenuItem();
                    pull.Header = $"Pull '{upstream}'";
                    pull.Click += (o, e) => {
                        Pull.Show(repo);
                        e.Handled = true;
                    };

                    menu.Items.Add(fastForward);
                    menu.Items.Add(pull);
                }

                menu.Items.Add(push);
            } else {
                var current = repo.CurrentBranch();

                var checkout = new MenuItem();
                checkout.Header = $"Checkout {branch.Name}";
                checkout.Click += (o, e) => {
                    Task.Run(() => repo.Checkout(node.Branch.Name));
                    e.Handled = true;
                };
                menu.Items.Add(checkout);
                menu.Items.Add(new Separator());
                menu.Items.Add(push);

                var merge = new MenuItem();
                merge.Header = $"Merge '{branch.Name}' into '{current.Name}'";
                merge.Click += (o, e) => {
                    Merge.Show(repo, branch.Name, current.Name);
                    e.Handled = true;
                };
                menu.Items.Add(merge);

                var rebase = new MenuItem();
                rebase.Header = $"Rebase '{current.Name}' on '{branch.Name}'";
                rebase.Click += (o, e) => {
                    Rebase.Show(repo, branch);
                    e.Handled = true;
                };
                menu.Items.Add(rebase);
            }

            if (branch.Kind != Git.Branch.Type.Normal) {
                menu.Items.Add(new Separator());

                var icon = new System.Windows.Shapes.Path();
                icon.Style = FindResource("Style.Icon") as Style;
                icon.Data = FindResource("Icon.Flow") as Geometry;
                icon.Width = 10;

                var finish = new MenuItem();
                finish.Header = $"Git Flow - Finish '{branch.Name}'";
                finish.Icon = icon;
                finish.Click += (o, e) => {
                    GitFlowFinishBranch.Show(repo, branch);
                    e.Handled = true;
                };

                menu.Items.Add(finish);
            } 

            var rename = new MenuItem();
            rename.Header = $"Rename '{branch.Name}'";
            rename.Click += (o, e) => {
                RenameBranch.Show(repo, branch);
                e.Handled = true;
            };
            menu.Items.Add(new Separator());
            menu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = $"Delete '{branch.Name}'";
            delete.IsEnabled = !branch.IsCurrent;
            delete.Click += (o, e) => {
                DeleteBranch.Show(repo, branch);
                e.Handled = true;
            };
            menu.Items.Add(delete);
            menu.Items.Add(new Separator());

            var createBranch = new MenuItem();
            createBranch.Header = "Create Branch";
            createBranch.Click += (o, e) => {
                CreateBranch.Show(repo, branch);
                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var createTag = new MenuItem();
            createTag.Header = "Create Tag";
            createTag.Click += (o, e) => {
                CreateTag.Show(repo, branch);
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new Separator());

            var copy = new MenuItem();
            copy.Header = "Copy Branch Name";
            copy.Click += (o, e) => {
                Clipboard.SetText(branch.Name);
                e.Handled = true;
            };
            menu.Items.Add(copy);

            menu.IsOpen = true;
            ev.Handled = true;
        }
        #endregion

        #region REMOTE_BRANCHES
        private void OpenRemote(object sender, RoutedEventArgs e) {
            Remote.Show(repo);
        }

        private void OpenRemoteContextMenu(RemoteNode node) {
            var fetch = new MenuItem();
            fetch.Header = $"Fetch '{node.Name}'";
            fetch.Click += (o, e) => {
                Fetch.Show(repo, node.Name);
                e.Handled = true;
            };

            var edit = new MenuItem();
            edit.Header = $"Edit '{node.Name}'";
            edit.Click += (o, e) => {
                var remotes = repo.Remotes();
                var found = remotes.Find(r => r.Name == node.Name);
                if (found != null) Remote.Show(repo, found);
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = $"Delete '{node.Name}'";
            delete.Click += (o, e) => {
                DeleteRemote.Show(repo, node.Name);
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = "Copy Remote URL";
            copy.Click += (o, e) => {
                var remotes = repo.Remotes();
                var found = remotes.Find(r => r.Name == node.Name);
                if (found != null) Clipboard.SetText(found.URL);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(fetch);
            menu.Items.Add(new Separator());
            menu.Items.Add(edit);
            menu.Items.Add(delete);
            menu.Items.Add(new Separator());
            menu.Items.Add(copy);
            menu.IsOpen = true;
        } 

        private void OpenRemoteBranchContextMenu(BranchNode node) {
            var branch = node.Branch;
            var current = repo.CurrentBranch();
            if (current == null) return;

            var checkout = new MenuItem();
            checkout.Header = $"Checkout '{branch.Name}'";
            checkout.Click += (o, e) => {
                var branches = repo.Branches();
                var tracked = null as Git.Branch;
                var upstream = $"refs/remotes/{branch.Name}";

                foreach (var b in branches) {
                    if (b.IsLocal && b.Upstream == upstream) {
                        tracked = b;
                        break;
                    }
                }

                if (tracked == null) {
                    CreateBranch.Show(repo, branch);
                } else if (!tracked.IsCurrent) {
                    Task.Run(() => repo.Checkout(tracked.Name));
                }

                e.Handled = true;
            };

            var pull = new MenuItem();
            pull.Header = $"Pull '{branch.Name}' into '{current.Name}'";
            pull.Click += (o, e) => {
                Pull.Show(repo, branch.Name);
                e.Handled = true;
            };

            var merge = new MenuItem();
            merge.Header = $"Merge '{branch.Name}' into '{current.Name}'";
            merge.Click += (o, e) => {
                Merge.Show(repo, branch.Name, current.Name);
                e.Handled = true;
            };

            var rebase = new MenuItem();
            rebase.Header = $"Rebase '{current.Name}' on '{branch.Name}'";
            rebase.Click += (o, e) => {
                Rebase.Show(repo, branch);
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = $"Delete '{branch.Name}'";
            delete.Click += (o, e) => {
                DeleteBranch.Show(repo, branch);
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Header = "Create New Branch";
            createBranch.Click += (o, e) => {
                CreateBranch.Show(repo, branch);
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Header = "Create New Tag";
            createTag.Click += (o, e) => {
                CreateTag.Show(repo, branch);
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = "Copy Branch Name";
            copy.Click += (o, e) => {
                Clipboard.SetText(branch.Name);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(checkout);
            menu.Items.Add(new Separator());
            menu.Items.Add(pull);
            menu.Items.Add(merge);
            menu.Items.Add(rebase);
            menu.Items.Add(new Separator());
            menu.Items.Add(delete);
            menu.Items.Add(new Separator());
            menu.Items.Add(createBranch);
            menu.Items.Add(createTag);
            menu.Items.Add(new Separator());
            menu.Items.Add(copy);
            menu.IsOpen = true;
        }

        private void RemoteBranchSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            var node = e.NewValue as BranchNode;
            if (node == null || node.Branch == null) return;
            repo.OnNavigateCommit?.Invoke(node.Branch.Head);
        }

        private void RemoteContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var remoteNode = (sender as TreeViewItem).DataContext as RemoteNode;
            if (remoteNode != null) {
                OpenRemoteContextMenu(remoteNode);
                ev.Handled = true;
                return;
            }

            var branchNode = (sender as TreeViewItem).DataContext as BranchNode;
            if (branchNode != null && branchNode.Branch != null) {
                OpenRemoteBranchContextMenu(branchNode);
                ev.Handled = true;
                return;
            }
        }
        #endregion

        #region TAGS
        private void OpenNewTag(object sender, RoutedEventArgs e) {
            CreateTag.Show(repo);
        }

        private void TagLostFocus(object sender, RoutedEventArgs e) {
            (sender as DataGrid).UnselectAll();
        }

        private void TagSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 1) {
                var item = e.AddedItems[0] as Git.Tag;
                repo.OnNavigateCommit?.Invoke(item.SHA);
            }
        }

        private void TagContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var tag = (sender as DataGrid).SelectedItem as Git.Tag;
            if (tag == null) return;

            var createBranch = new MenuItem();
            createBranch.Header = "Create New Branch";
            createBranch.Click += (o, ev) => {
                CreateBranch.Show(repo, tag);
                ev.Handled = true;
            };

            var pushTag = new MenuItem();
            pushTag.Header = $"Push '{tag.Name}'";
            pushTag.Click += (o, ev) => {
                PushTag.Show(repo, tag);
                ev.Handled = true;
            };

            var deleteTag = new MenuItem();
            deleteTag.Header = $"Delete '{tag.Name}'";
            deleteTag.Click += (o, ev) => {
                DeleteTag.Show(repo, tag);
                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = "Copy Name";
            copy.Click += (o, ev) => {
                Clipboard.SetText(tag.Name);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(createBranch);
            menu.Items.Add(new Separator());
            menu.Items.Add(pushTag);
            menu.Items.Add(deleteTag);
            menu.Items.Add(new Separator());
            menu.Items.Add(copy);
            menu.IsOpen = true;

            e.Handled = true;
        }
        #endregion

        #region SUBMODULES
        private void OpenAddSubmodule(object sender, RoutedEventArgs e) {
            AddSubmodule.Show(repo);
        }

        private void UpdateSubmodule(object sender, RoutedEventArgs e) {
            Waiting.Show(() => {
                var errs = repo.RunCommand("submodule update", PopupManager.UpdateStatus, true);
                if (errs != null) App.RaiseError(errs);
            });
        }

        private void SubmoduleLostFocus(object sender, RoutedEventArgs e) {
            (sender as DataGrid).UnselectAll();
        }

        private void SubmoduleContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var path = (sender as DataGrid).SelectedItem as string;
            if (path == null) return;

            var open = new MenuItem();
            open.Header = "Open Submodule Repository";
            open.Click += (o, ev) => {
                var sub = new Git.Repository();
                sub.Path = Path.Combine(repo.Path, path);
                sub.Name = Path.GetFileName(path);
                sub.Parent = repo;
                sub.Open();

                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = "Copy Relative Path";
            copy.Click += (o, ev) => {
                Clipboard.SetText(path);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(open);
            menu.Items.Add(copy);
            menu.IsOpen = true;

            e.Handled = true;
        }

        private void SubmoduleMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var path = (sender as DataGridRow).DataContext as string;
            if (path == null) return;

            var sub = new Git.Repository();
            sub.Path = Path.Combine(repo.Path, path);
            sub.Name = Path.GetFileName(path);
            sub.Parent = repo;
            sub.Open();
        }
        #endregion

        #region TREES
        private TreeViewItem FindTreeViewItem(ItemsControl item, BranchNode node) {
            if (item == null) return null;

            var data = item.DataContext as BranchNode;
            if (data == node) return item as TreeViewItem;

            for (int i = 0; i < item.Items.Count; i++) {
                var childContainer = item.ItemContainerGenerator.ContainerFromIndex(i) as ItemsControl;
                var child = FindTreeViewItem(childContainer, node);
                if (child != null) return child;
            }

            return null;
        }

        private void TreeLostFocus(object sender, RoutedEventArgs e) {
            var tree = sender as TreeView;
            var remote = tree.SelectedItem as RemoteNode;
            if (remote != null) {
                var remoteItem = tree.ItemContainerGenerator.ContainerFromItem(remote) as TreeViewItem;
                if (remoteItem != null) remoteItem.IsSelected = false;
                return;
            }

            var node = tree.SelectedItem as BranchNode;
            if (node == null) return;

            var item = FindTreeViewItem(tree, node);
            if (item != null) item.IsSelected = false;
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

        #region FILETER
        private void FilterChanged(object sender, RoutedEventArgs e) {
            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            if (toggle.DataContext is BranchNode) {
                var branch = (toggle.DataContext as BranchNode).Branch;
                if (branch == null) return;

                if (toggle.IsChecked == true) {
                    if (!repo.LogFilters.Contains(branch.FullName)) {
                        repo.LogFilters.Add(branch.FullName);
                    }
                    if (!string.IsNullOrEmpty(branch.Upstream) && !repo.LogFilters.Contains(branch.Upstream)) {
                        repo.LogFilters.Add(branch.Upstream);
                        UpdateBranches(false);
                    }
                } else {
                    repo.LogFilters.Remove(branch.FullName);
                    if (!string.IsNullOrEmpty(branch.Upstream)) {
                        repo.LogFilters.Remove(branch.Upstream);
                        UpdateBranches(false);
                    }
                }
            }

            if (toggle.DataContext is Git.Tag) {
                var tag = toggle.DataContext as Git.Tag;

                if (toggle.IsChecked == true) {
                    if (!repo.LogFilters.Contains(tag.Name)) {
                        repo.LogFilters.Add(tag.Name);
                    }
                } else {
                    repo.LogFilters.Remove(tag.Name);
                }
            }

            UpdateHistories();
        }
        #endregion
    }
}
