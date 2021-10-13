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

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     仓库操作主界面
    /// </summary>
    public partial class Dashboard : UserControl, Controls.IPopupContainer {
        private Models.Repository repo = null;
        private List<BranchNode> localBranches = new List<BranchNode>();
        private List<BranchNode> remoteBranches = new List<BranchNode>();
        private bool isFirstLoaded = false;

        /// <summary>
        ///     节点类型
        /// </summary>
        public enum BranchNodeType {
            Remote,
            Branch,
            Folder,
        }

        /// <summary>
        ///     分支节点
        /// </summary>
        public class BranchNode {
            public string Name { get; set; } = "";
            public bool IsExpanded { get; set; } = false;
            public bool IsFiltered { get; set; } = false;
            public BranchNodeType Type { get; set; } = BranchNodeType.Folder;
            public object Data { get; set; } = null;
            public List<BranchNode> Children { get; set; } = new List<BranchNode>();

            public string UpstreamTrackStatus {
                get { return Type == BranchNodeType.Branch ? (Data as Models.Branch).UpstreamTrackStatus : ""; }
            }

            public bool IsCurrent {
                get { return Type == BranchNodeType.Branch ? (Data as Models.Branch).IsCurrent : false; }
            }
        }

        public Dashboard(Models.Repository repo) {
            this.repo = repo;

            InitializeComponent();
            InitPages();

            var watcher = Models.Watcher.Get(repo.Path);
            watcher.Navigate += NavigateTo;
            watcher.BranchChanged += UpdateBranches;
            watcher.BranchChanged += UpdateCommits;
            watcher.WorkingCopyChanged += UpdateWorkingCopy;
            watcher.StashChanged += UpdateStashes;
            watcher.TagChanged += UpdateTags;
            watcher.TagChanged += UpdateCommits;
            watcher.SubmoduleChanged += UpdateSubmodules;
            watcher.SubTreeChanged += UpdateSubTrees;

            IsVisibleChanged += OnVisibleChanged;
            Unloaded += (o, e) => {
                localBranches.Clear();
                remoteBranches.Clear();
                localBranchTree.ItemsSource = localBranches;
                remoteBranchTree.ItemsSource = remoteBranches;
                tagList.ItemsSource = new List<Models.Tag>();
                submoduleList.ItemsSource = new List<string>();
            };
        }

        #region POPUP
        public void Show(Controls.PopupWidget widget) {
            popup.Show(widget);
        }

        public void ShowAndStart(Controls.PopupWidget widget) {
            popup.ShowAndStart(widget);
        }

        public void UpdateProgress(string message) {
            popup.UpdateProgress(message);
        }
        #endregion

        #region DATA
        public void Refresh() {
            UpdateBranches();
            UpdateWorkingCopy();
            UpdateStashes();
            UpdateTags();
            UpdateSubmodules();
            UpdateSubTrees();
            UpdateCommits();
        }

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs ev) {
            if (IsVisible && !isFirstLoaded) {
                isFirstLoaded = true;
                Refresh();
            }
        }

        private void NavigateTo(string commitId) {
            if (!isFirstLoaded) return;

            workspace.SelectedIndex = 0;
            (pages.Get("histories") as Histories).NavigateTo(commitId);
        }

        private void BackupBranchExpandState(Dictionary<string, bool> states, List<BranchNode> nodes, string prefix) {
            foreach (var node in nodes) {
                if (node.Type != BranchNodeType.Branch) {
                    var id = string.Concat(prefix, "/", node.Name);
                    states[id] = node.IsExpanded;
                    BackupBranchExpandState(states, node.Children, id);
                }
            }
        }

        private void MakeBranchNode(Models.Branch branch, List<BranchNode> roots, Dictionary<string, BranchNode> folders, Dictionary<string, bool> states, string prefix) {
            var subs = branch.Name.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (subs.Length == 1) {
                var node = new BranchNode() {
                    Name = subs[0],
                    IsExpanded = false,
                    IsFiltered = repo.Filters.Contains(branch.FullName),
                    Type = BranchNodeType.Branch,
                    Data = branch,
                };
                roots.Add(node);
                return;
            }

            BranchNode lastFolder = null;
            string path = prefix;
            for (int i = 0; i < subs.Length - 1; i++) {
                path = string.Concat(path, "/", subs[i]);
                if (folders.ContainsKey(path)) {
                    lastFolder = folders[path];
                } else if (lastFolder == null) {
                    lastFolder = new BranchNode() {
                        Name = subs[i],
                        IsExpanded = states.ContainsKey(path) ? states[path] : false,
                        Type = BranchNodeType.Folder,
                    };
                    roots.Add(lastFolder);
                    folders.Add(path, lastFolder);
                } else {
                    var folder = new BranchNode() {
                        Name = subs[i],
                        IsExpanded = states.ContainsKey(path) ? states[path] : false,
                        Type = BranchNodeType.Folder,
                    };
                    folders.Add(path, folder);
                    lastFolder.Children.Add(folder);
                    lastFolder = folder;
                }
            }

            BranchNode last = new BranchNode() {
                Name = subs.Last(),
                IsExpanded = false,
                IsFiltered = repo.Filters.Contains(branch.FullName),
                Type = BranchNodeType.Branch,
                Data = branch,
            };
            lastFolder.Children.Add(last);
        }

        private void SortBranches(List<BranchNode> nodes) {
            nodes.Sort((l, r) => {
                if (l.Type == r.Type) {
                    return l.Name.CompareTo(r.Name);
                } else {
                    return (int)(l.Type) - (int)(r.Type);
                }
            });

            foreach (var node in nodes) SortBranches(node.Children);
        }

        private void UpdateBranches() {
            if (!isFirstLoaded) return;

            Task.Run(() => {
                repo.Branches = new Commands.Branches(repo.Path).Result();
                repo.Remotes = new Commands.Remotes(repo.Path).Result();

                var states = new Dictionary<string, bool>();
                BackupBranchExpandState(states, localBranches, "locals");
                BackupBranchExpandState(states, remoteBranches, "remotes");

                var folders = new Dictionary<string, BranchNode>();
                localBranches = new List<BranchNode>();
                remoteBranches = new List<BranchNode>();

                foreach (var r in repo.Remotes) {
                    var fullName = $"remotes/{r.Name}";
                    var node = new BranchNode() {
                        Name = r.Name,
                        IsExpanded = states.ContainsKey(fullName) ? states[fullName] : false,
                        Type = BranchNodeType.Remote,
                        Data = r,
                    };
                    remoteBranches.Add(node);
                    folders.Add(fullName, node);
                }

                foreach (var b in repo.Branches) {
                    if (b.IsLocal) {
                        MakeBranchNode(b, localBranches, folders, states, "locals");
                    } else {
                        var r = remoteBranches.Find(x => x.Name == b.Remote);
                        if (r != null) MakeBranchNode(b, r.Children, folders, states, $"remotes/{b.Remote}");
                    }
                }

                SortBranches(localBranches);
                SortBranches(remoteBranches);

                Dispatcher.Invoke(() => {
                    localBranchTree.ItemsSource = localBranches;
                    remoteBranchTree.ItemsSource = remoteBranches;
                });
            });
        }

        private void UpdateWorkingCopy() {
            if (!isFirstLoaded) return;

            Task.Run(() => {
                var changes = new Commands.LocalChanges(repo.Path).Result();
                Dispatcher.Invoke(() => {
                    badgeLocalChanges.Label = $"{changes.Count}";
                    (pages.Get("working_copy") as WorkingCopy).SetData(changes);
                    UpdateMergeBar(changes);
                });
            });
        }

        private void UpdateStashes() {
            if (!isFirstLoaded) return;

            Task.Run(() => {
                var stashes = new Commands.Stashes(repo.Path).Result();
                Dispatcher.Invoke(() => {
                    badgeStashes.Label = $"{stashes.Count}";
                    (pages.Get("stashes") as Stashes).SetData(stashes);
                });
            });
        }

        private void UpdateTags() {
            if (!isFirstLoaded) return;

            Task.Run(() => {
                var tags = new Commands.Tags(repo.Path).Result();
                foreach (var t in tags) t.IsFiltered = repo.Filters.Contains(t.Name);
                Dispatcher.Invoke(() => {
                    txtTagCount.Text = $"({tags.Count})";
                    tagList.ItemsSource = tags;
                });
            });
        }

        private void UpdateSubmodules() {
            if (!isFirstLoaded) return;

            Task.Run(() => {
                var submodules = new Commands.Submodules(repo.Path).Result();
                Dispatcher.Invoke(() => {
                    txtSubmoduleCount.Text = $"({submodules.Count})";
                    submoduleList.ItemsSource = submodules;
                });
            });
        }

        private void UpdateSubTrees() {
            if (!isFirstLoaded) return;

            Dispatcher.Invoke(() => {
                txtSubTreeCount.Text = $"({repo.SubTrees.Count})";
                subTreeList.ItemsSource = null;
                subTreeList.ItemsSource = repo.SubTrees;
            });
        }

        private void UpdateCommits() {
            if (!isFirstLoaded) return;

            (pages.Get("histories") as Histories).UpdateCommits();
        }
        #endregion

        #region TOOLBAR_COMMANDS
        private MenuItem CreateMenuItem(string icon, string header, Action click) {
            var ret = new MenuItem();
            ret.Header = App.Text(header);
            ret.Click += (o, e) => {
                click();
                e.Handled = true;
            };

            if (!string.IsNullOrEmpty(icon)) {
                var geo = new System.Windows.Shapes.Path();
                geo.Data = FindResource(icon) as Geometry;
                geo.VerticalAlignment = VerticalAlignment.Center;
                geo.Width = 12;
                geo.Height = 12;

                ret.Icon = geo;
            }

            return ret;
        }

        private void OpenExternal(object sender, RoutedEventArgs e) {
            var btn = sender as Controls.IconButton;
            if (btn == null) return;

            if (btn.ContextMenu != null) {
                btn.ContextMenu.IsOpen = true;
                e.Handled = true;
                return;
            }

            var menu = new ContextMenu();
            menu.PlacementTarget = btn;
            menu.Placement = PlacementMode.Bottom;
            menu.StaysOpen = false;
            menu.Focusable = true;

            menu.Items.Add(CreateMenuItem("Icon.Folder.Open", "Dashboard.Explore", () => {
                Process.Start("explorer", repo.Path);
            }));

            menu.Items.Add(CreateMenuItem("Icon.Terminal", "Dashboard.Terminal", () => {
                var bash = Path.Combine(Models.Preference.Instance.Git.Path, "..", "bash.exe");
                if (!File.Exists(bash)) {
                    Models.Exception.Raise(App.Text("MissingBash"));
                    return;
                }

                if (Models.Preference.Instance.General.UseWindowsTerminal) {
                    Process.Start(new ProcessStartInfo {
                        WorkingDirectory = repo.Path,
                        FileName = "wt",
                        Arguments = $"-d \"{repo.Path}\" \"{bash}\"",
                        UseShellExecute = false,
                    });
                } else {
                    Process.Start(new ProcessStartInfo {
                        WorkingDirectory = repo.Path,
                        FileName = bash,
                        UseShellExecute = true,
                    });
                }
            }));

            var vscode = Models.ExecutableFinder.Find("code.cmd");
            if (vscode != null) {
                vscode = Path.Combine(Path.GetDirectoryName(vscode), "..", "Code.exe");
                menu.Items.Add(CreateMenuItem("Icon.VSCode", "Dashboard.VSCode", () => {
                    Process.Start(new ProcessStartInfo {
                        WorkingDirectory = repo.Path,
                        FileName = vscode,
                        Arguments = $"\"{repo.Path}\"",
                        UseShellExecute = false,
                    });
                }));
            }

            btn.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void TriggerRefresh(object sender, RoutedEventArgs e) {
            Refresh();
            e.Handled = true;
        }

        private void OpenFetch(object sender, RoutedEventArgs e) {
            if (repo.Remotes.Count == 0) {
                Models.Exception.Raise("No remotes added to this repository!!!");
                return;
            }

            new Popups.Fetch(repo, null).Show();
            e.Handled = true;
        }

        private void OpenPull(object sender, RoutedEventArgs e) {
            if (repo.Remotes.Count == 0) {
                Models.Exception.Raise("No remotes added to this repository!!!");
                return;
            }

            new Popups.Pull(repo, null).Show();
            e.Handled = true;
        }

        private void OpenPush(object sender, RoutedEventArgs e) {
            if (repo.Remotes.Count == 0) {
                Models.Exception.Raise("No remotes added to this repository!!!");
                return;
            }

            new Popups.Push(repo, null).Show();
            e.Handled = true;
        }

        private void OpenStash(object sender, RoutedEventArgs e) {
            new Popups.Stash(repo.Path, null).Show();
            e.Handled = true;
        }

        private void OpenApply(object sender, RoutedEventArgs e) {
            new Popups.Apply(repo.Path).Show();
            e.Handled = true;
        }

        public void OpenSearch(object sender, RoutedEventArgs e) {
            if (popup.IsLocked) return;
            popup.Close();

            workspace.SelectedIndex = 0;
            (pages.Get("histories") as Histories).ToggleSearch();
        }

        private void ChangeOrientation(object sender, RoutedEventArgs e) {
            if (!IsLoaded) return;

            (pages.Get("histories") as Histories)?.ChangeOrientation();
        }

        private void OpenConfigure(object sender, RoutedEventArgs e) {
            new Popups.Configure(repo.Path).Show();
            e.Handled = true;
        }
        #endregion

        #region PAGES
        private void InitPages() {
            pages.Add("histories", new Histories(repo));
            pages.Add("working_copy", new WorkingCopy(repo));
            pages.Add("stashes", new Stashes(repo.Path));
            pages.Goto("histories");
        }

        private void OnPageSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (pages == null) return;

            switch (workspace.SelectedIndex) {
            case 0: pages.Goto("histories"); break;
            case 1: pages.Goto("working_copy"); break;
            case 2: pages.Goto("stashes"); break;
            }

            if (mergeNavigator.Visibility == Visibility.Visible) {
                btnResolve.Visibility = workspace.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        #endregion

        #region BRANCHES
        private void OpenGitFlowPanel(object sender, RoutedEventArgs ev) {
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

            if (repo.GitFlow.IsEnabled) {
                var startFeature = new MenuItem();
                startFeature.Header = App.Text("GitFlow.StartFeature");
                startFeature.Click += (o, e) => {
                    new Popups.GitFlowStart(repo, Models.GitFlowBranchType.Feature).Show();
                    e.Handled = true;
                };

                var startRelease = new MenuItem();
                startRelease.Header = App.Text("GitFlow.StartRelease");
                startRelease.Click += (o, e) => {
                    new Popups.GitFlowStart(repo, Models.GitFlowBranchType.Release).Show();
                    e.Handled = true;
                };

                var startHotfix = new MenuItem();
                startHotfix.Header = App.Text("GitFlow.StartHotfix");
                startHotfix.Click += (o, e) => {
                    new Popups.GitFlowStart(repo, Models.GitFlowBranchType.Hotfix).Show();
                    e.Handled = true;
                };

                button.ContextMenu.Items.Add(startFeature);
                button.ContextMenu.Items.Add(startRelease);
                button.ContextMenu.Items.Add(startHotfix);
            } else {
                var init = new MenuItem();
                init.Header = App.Text("GitFlow.Init");
                init.Click += (o, e) => {
                    new Popups.InitGitFlow(repo).Show();
                    e.Handled = true;
                };
                button.ContextMenu.Items.Add(init);
            }

            button.ContextMenu.IsOpen = true;
            ev.Handled = true;
        }

        private void OpenNewBranch(object sender, RoutedEventArgs e) {
            var current = repo.Branches.Find(x => x.IsCurrent);
            if (current != null) {
                new Popups.CreateBranch(repo, current).Show();
            } else {
                Models.Exception.Raise(App.Text("CreateBranch.Idle"));
            }
            e.Handled = true;
        }

        private void OpenAddRemote(object sender, RoutedEventArgs e) {
            new Popups.Remote(repo, null).Show();
            e.Handled = true;
        }

        private void OnTreeLostFocus(object sender, RoutedEventArgs e) {
            var tree = sender as Controls.Tree;
            var child = FocusManager.GetFocusedElement(leftPanel);
            if (child != null && tree.IsAncestorOf(child as DependencyObject)) return;
            tree.UnselectAll();
        }

        private void OnTreeSelectionChanged(object sender, RoutedEventArgs e) {
            var tree = sender as Controls.Tree;
            if (tree.Selected.Count == 0) return;

            var node = tree.Selected[0] as BranchNode;
            if (node.Type == BranchNodeType.Branch) NavigateTo((node.Data as Models.Branch).Head);
        }

        private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e) {
            var item = sender as Controls.TreeItem;
            if (item == null) return;

            var node = item.DataContext as BranchNode;
            if (node == null || node.Type != BranchNodeType.Branch) return;

            var branch = node.Data as Models.Branch;
            if (!branch.IsLocal || branch.IsCurrent) return;

            new Popups.Checkout(repo.Path, branch.Name).ShowAndStart();
        }

        private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var item = sender as Controls.TreeItem;
            if (item == null) return;

            var node = item.DataContext as BranchNode;
            if (node == null || node.Type == BranchNodeType.Folder) return;

            var menu = new ContextMenu();
            if (node.Type == BranchNodeType.Remote) {
                FillRemoteContextMenu(menu, node.Data as Models.Remote);
            } else {
                var branch = node.Data as Models.Branch;
                if (branch.IsLocal) {
                    FillLocalBranchContextMenu(menu, branch);
                } else {
                    FillRemoteBranchContextMenu(menu, branch);
                }
            }

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void FillLocalBranchContextMenu(ContextMenu menu, Models.Branch branch) {
            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", branch.Name);
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += (o, e) => {
                new Popups.Push(repo, branch).Show();
                e.Handled = true;
            };

            if (branch.IsCurrent) {
                var discard = new MenuItem();
                discard.Header = App.Text("BranchCM.DiscardAll");
                discard.Click += (o, e) => {
                    new Popups.Discard(repo.Path, null).Show();
                    e.Handled = true;
                };

                menu.Items.Add(discard);
                menu.Items.Add(new Separator());

                if (!string.IsNullOrEmpty(branch.Upstream)) {
                    var upstream = branch.Upstream.Substring(13);
                    var fastForward = new MenuItem();
                    fastForward.Header = App.Text("BranchCM.FastForward", upstream);
                    fastForward.IsEnabled = !string.IsNullOrEmpty(branch.UpstreamTrackStatus);
                    fastForward.Click += (o, e) => {
                        new Popups.Merge(repo.Path, upstream, branch.Name).ShowAndStart();
                        e.Handled = true;
                    };

                    var pull = new MenuItem();
                    pull.Header = App.Text("BranchCM.Pull", upstream);
                    pull.IsEnabled = !string.IsNullOrEmpty(branch.UpstreamTrackStatus);
                    pull.Click += (o, e) => {
                        new Popups.Pull(repo, null).Show();
                        e.Handled = true;
                    };

                    menu.Items.Add(fastForward);
                    menu.Items.Add(pull);
                }

                menu.Items.Add(push);
            } else {
                var current = repo.Branches.Find(x => x.IsCurrent);

                var checkout = new MenuItem();
                checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
                checkout.Click += (o, e) => {
                    new Popups.Checkout(repo.Path, branch.Name).ShowAndStart();
                    e.Handled = true;
                };
                menu.Items.Add(checkout);
                menu.Items.Add(new Separator());
                menu.Items.Add(push);

                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", branch.Name, current.Name);
                merge.Click += (o, e) => {
                    new Popups.Merge(repo.Path, branch.Name, current.Name).Show();
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = App.Text("BranchCM.Rebase", current.Name, branch.Name);
                rebase.Click += (o, e) => {
                    new Popups.Rebase(repo.Path, current.Name, branch).Show();
                    e.Handled = true;
                };

                menu.Items.Add(merge);
                menu.Items.Add(rebase);
            }

            var type = repo.GitFlow.GetBranchType(branch.Name);
            if (type != Models.GitFlowBranchType.None) {
                var flowIcon = new System.Windows.Shapes.Path();
                flowIcon.Data = FindResource("Icon.Flow") as Geometry;
                flowIcon.Width = 10;

                var finish = new MenuItem();
                finish.Header = App.Text("BranchCM.Finish", branch.Name);
                finish.Icon = flowIcon;
                finish.Click += (o, e) => {
                    new Popups.GitFlowFinish(repo, branch.Name, type).Show();
                    e.Handled = true;
                };
                menu.Items.Add(new Separator());
                menu.Items.Add(finish);
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Click += (o, e) => {
                new Popups.RenameBranch(repo, branch.Name).Show();
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.IsEnabled = !branch.IsCurrent;
            delete.Click += (o, e) => {
                new Popups.DeleteBranch(repo.Path, branch.Name).Show();
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) => {
                new Popups.CreateBranch(repo, branch).Show();
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) => {
                new Popups.CreateTag(repo, branch).Show();
                e.Handled = true;
            };

            menu.Items.Add(new Separator());
            menu.Items.Add(rename);
            menu.Items.Add(delete);
            menu.Items.Add(new Separator());
            menu.Items.Add(createBranch);
            menu.Items.Add(createTag);
            menu.Items.Add(new Separator());

            var remoteBranches = repo.Branches.Where(x => !x.IsLocal).ToList();
            if (remoteBranches.Count > 0) {
                var trackingIcon = new System.Windows.Shapes.Path();
                trackingIcon.Data = FindResource("Icon.Branch") as Geometry;
                trackingIcon.VerticalAlignment = VerticalAlignment.Bottom;
                trackingIcon.Width = 10;

                var currentTrackingIcon = new System.Windows.Shapes.Path();
                currentTrackingIcon.Data = FindResource("Icon.Check") as Geometry;
                currentTrackingIcon.VerticalAlignment = VerticalAlignment.Center;
                currentTrackingIcon.Width = 10;

                var tracking = new MenuItem();
                tracking.Header = App.Text("BranchCM.Tracking");
                tracking.Icon = trackingIcon;

                foreach (var b in remoteBranches) {
                    var upstream = b.FullName.Replace("refs/remotes/", "");
                    var target = new MenuItem();
                    target.Header = upstream;
                    if (branch.Upstream == b.FullName) target.Icon = currentTrackingIcon;
                    target.Click += (o, e) => {
                        new Commands.Branch(repo.Path, branch.Name).SetUpstream(upstream);
                        UpdateBranches();
                        e.Handled = true;
                    };
                    tracking.Items.Add(target);
                }

                var unsetUpstream = new MenuItem();
                unsetUpstream.Header = App.Text("BranchCM.UnsetUpstream");
                unsetUpstream.Click += (_, e) => {
                    new Commands.Branch(repo.Path, branch.Name).SetUpstream(string.Empty);
                    UpdateBranches();
                    e.Handled = true;
                };
                tracking.Items.Add(new Separator());
                tracking.Items.Add(unsetUpstream);

                menu.Items.Add(tracking);
            }

            var archive = new MenuItem();
            archive.Header = App.Text("Archive");
            archive.Click += (o, e) => {
                new Popups.Archive(repo.Path, branch).Show();
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new Separator());

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Click += (o, e) => {
                Clipboard.SetDataObject(branch.Name, true);
                e.Handled = true;
            };
            menu.Items.Add(copy);
        }

        private void FillRemoteContextMenu(ContextMenu menu, Models.Remote remote) {
            var fetch = new MenuItem();
            fetch.Header = App.Text("RemoteCM.Fetch", remote.Name);
            fetch.Click += (o, e) => {
                new Popups.Fetch(repo, remote.Name).Show();
                e.Handled = true;
            };

            var edit = new MenuItem();
            edit.Header = App.Text("RemoteCM.Edit", remote.Name);
            edit.Click += (o, e) => {
                new Popups.Remote(repo, remote).Show();
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("RemoteCM.Delete", remote.Name);
            delete.Click += (o, e) => {
                new Popups.DeleteRemote(repo.Path, remote.Name).Show();
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("RemoteCM.CopyURL");
            copy.Click += (o, e) => {
                Clipboard.SetDataObject(remote.URL, true);
                e.Handled = true;
            };

            menu.Items.Add(fetch);
            menu.Items.Add(new Separator());
            menu.Items.Add(edit);
            menu.Items.Add(delete);
            menu.Items.Add(new Separator());
            menu.Items.Add(copy);
        }

        private void FillRemoteBranchContextMenu(ContextMenu menu, Models.Branch branch) {
            var current = repo.Branches.Find(x => x.IsCurrent);

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
            checkout.Click += (o, e) => {
                foreach (var b in repo.Branches) {
                    if (b.IsLocal && b.Upstream == branch.FullName) {
                        if (b.IsCurrent) return;
                        new Popups.Checkout(repo.Path, b.Name).ShowAndStart();
                        return;
                    }
                }

                new Popups.CreateBranch(repo, branch).Show();
                e.Handled = true;
            };
            menu.Items.Add(checkout);
            menu.Items.Add(new Separator());

            if (current != null) {
                var pull = new MenuItem();
                pull.Header = App.Text("BranchCM.PullInto", branch.Name, current.Name);
                pull.Click += (o, e) => {
                    new Popups.Pull(repo, branch).Show();
                    e.Handled = true;
                };

                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", branch.Name, current.Name);
                merge.Click += (o, e) => {
                    new Popups.Merge(repo.Path, $"{branch.Remote}/{branch.Name}", current.Name).Show();
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = App.Text("BranchCM.Rebase", current.Name, branch.Name);
                rebase.Click += (o, e) => {
                    new Popups.Rebase(repo.Path, current.Name, branch).Show();
                    e.Handled = true;
                };

                menu.Items.Add(pull);
                menu.Items.Add(merge);
                menu.Items.Add(rebase);
                menu.Items.Add(new Separator());
            }

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.Click += (o, e) => {
                new Popups.DeleteBranch(repo.Path, branch.Name, branch.Remote)
                    .Then(() => {
                        repo.Branches.FindAll(item => item.Upstream == branch.FullName).ForEach(item =>
                            new Commands.Branch(repo.Path, item.Name).SetUpstream(string.Empty));
                    })
                    .Show();
                e.Handled = true;
            };

            var createBranch = new MenuItem();
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, e) => {
                new Popups.CreateBranch(repo, branch).Show();
                e.Handled = true;
            };

            var createTag = new MenuItem();
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (o, e) => {
                new Popups.CreateTag(repo, branch).Show();
                e.Handled = true;
            };

            var archive = new MenuItem();
            archive.Header = App.Text("Archive");
            archive.Click += (o, e) => {
                new Popups.Archive(repo.Path, branch).Show();
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Click += (o, e) => {
                Clipboard.SetDataObject(branch.Remote + "/" + branch.Name, true);
                e.Handled = true;
            };

            menu.Items.Add(delete);
            menu.Items.Add(new Separator());
            menu.Items.Add(createBranch);
            menu.Items.Add(createTag);
            menu.Items.Add(new Separator());
            menu.Items.Add(archive);
            menu.Items.Add(new Separator());
            menu.Items.Add(copy);
        }
        #endregion

        #region TAGS
        private void OpenNewTag(object sender, RoutedEventArgs e) {
            new Popups.CreateTag(repo, repo.Branches.Find(x => x.IsCurrent)).Show();
            e.Handled = true;
        }

        private void OnTagsLostFocus(object sender, RoutedEventArgs e) {
            tagList.SelectedItem = null;
        }

        private void OnTagSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var tag = tagList.SelectedItem as Models.Tag;
            if (tag != null) NavigateTo(tag.SHA);
        }

        private void OnTagContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var tag = tagList.SelectedItem as Models.Tag;
            if (tag == null) return;

            var createBranch = new MenuItem();
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (o, ev) => {
                new Popups.CreateBranch(repo, tag).Show();
                ev.Handled = true;
            };

            var pushTag = new MenuItem();
            pushTag.Header = App.Text("TagCM.Push", tag.Name);
            pushTag.IsEnabled = repo.Remotes.Count > 0;
            pushTag.Click += (o, ev) => {
                new Popups.PushTag(repo, tag.Name).Show();
                ev.Handled = true;
            };

            var deleteTag = new MenuItem();
            deleteTag.Header = App.Text("TagCM.Delete", tag.Name);
            deleteTag.Click += (o, ev) => {
                new Popups.DeleteTag(repo.Path, tag.Name).Show();
                ev.Handled = true;
            };

            var archive = new MenuItem();
            archive.Header = App.Text("Archive");
            archive.Click += (o, ev) => {
                new Popups.Archive(repo.Path, tag).Show();
                ev.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.Copy");
            copy.Click += (o, ev) => {
                Clipboard.SetDataObject(tag.Name, true);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(createBranch);
            menu.Items.Add(new Separator());
            menu.Items.Add(pushTag);
            menu.Items.Add(deleteTag);
            menu.Items.Add(new Separator());
            menu.Items.Add(archive);
            menu.Items.Add(new Separator());
            menu.Items.Add(copy);
            menu.IsOpen = true;
            e.Handled = true;
        }
        #endregion

        #region SUBMODULES
        private void OpenAddSubmodule(object sender, RoutedEventArgs e) {
            new Popups.AddSubmodule(repo.Path).Show();
            e.Handled = true;
        }

        private async void UpdateSubmodules(object sender, RoutedEventArgs e) {
            iconUpdateSubmodule.IsAnimating = true;
            Models.Watcher.SetEnabled(repo.Path, false);
            await Task.Run(() => new Commands.Submodule(repo.Path).Update());
            Models.Watcher.SetEnabled(repo.Path, true);
            iconUpdateSubmodule.IsAnimating = false;
            e.Handled = true;
        }

        private void OnSubmoduleContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var submodule = submoduleList.SelectedItem as string;
            if (submodule == null) return;

            var copy = new MenuItem();
            copy.Header = App.Text("Submodule.CopyPath");
            copy.Click += (o, ev) => {
                Clipboard.SetDataObject(submodule, true);
                ev.Handled = true;
            };

            var rm = new MenuItem();
            rm.Header = App.Text("Submodule.Remove");
            rm.Click += (o, ev) => {
                new Popups.DeleteSubmodule(repo.Path, submodule).Show();
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Items.Add(rm);
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OnSubmoduleMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var submodule = submoduleList.SelectedItem as string;
            if (submodule == null) return;

            var hitted = (e.OriginalSource as FrameworkElement).DataContext as string;
            if (hitted == null || hitted != submodule) return;

            var sub = new Models.Repository();
            sub.Path = Path.GetFullPath(Path.Combine(repo.Path, submodule));
            sub.GitDir = new Commands.QueryGitDir(sub.Path).Result();
            sub.Name = repo.Name + " : " + Path.GetFileName(submodule);

            Models.Watcher.Open(sub);
            e.Handled = true;
        }
        #endregion

        #region SUBTREES
        private void OpenAddSubTree(object sender, RoutedEventArgs e) {
            new Popups.AddSubTree(repo).Show();
            e.Handled = true;
        }

        private void OnSubTreeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var subtree = subTreeList.SelectedItem as Models.SubTree;
            if (subtree == null) return;

            var edit = new MenuItem();
            edit.Header = App.Text("SubTree.Edit");
            edit.Click += (o, ev) => {
                new Popups.EditSubTree(repo, subtree.Prefix).Show();
                ev.Handled = true;
            };

            var unlink = new MenuItem();
            unlink.Header = App.Text("SubTree.Unlink");
            unlink.Click += (o, ev) => {
                new Popups.UnlinkSubTree(repo, subtree.Prefix).Show();
                ev.Handled = true;
            };

            var pull = new MenuItem();
            pull.Header = App.Text("SubTree.Pull");
            pull.Click += (o, ev) => {
                new Popups.SubTreePull(repo.Path, subtree).Show();
                ev.Handled = true;
            };

            var push = new MenuItem();
            push.Header = App.Text("SubTree.Push");
            push.Click += (o, ev) => {
                new Popups.SubTreePush(repo.Path, subtree).Show();
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(edit);
            menu.Items.Add(unlink);
            menu.Items.Add(new Separator());
            menu.Items.Add(pull);
            menu.Items.Add(push);
            menu.IsOpen = true;
            e.Handled = true;
        }
        #endregion

        #region FILTERS
        private void OnFilterChanged(object sender, RoutedEventArgs e) {
            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            var filter = "";
            var changed = false;

            if (toggle.DataContext is BranchNode) {
                var branch = (toggle.DataContext as BranchNode).Data as Models.Branch;
                if (branch == null) return;
                filter = branch.FullName;
            } else if (toggle.DataContext is Models.Tag) {
                filter = (toggle.DataContext as Models.Tag).Name;
            }

            if (toggle.IsChecked == true) {
                if (!repo.Filters.Contains(filter)) {
                    repo.Filters.Add(filter);
                    changed = true;
                }
            } else {
                if (repo.Filters.Contains(filter)) {
                    repo.Filters.Remove(filter);
                    changed = true;
                }
            }

            if (changed) (pages.Get("histories") as Histories).UpdateCommits();
        }
        #endregion

        #region MERGE_BAR
        private void UpdateMergeBar(List<Models.Change> changes) {
            if (File.Exists(Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD"))) {
                txtConflictTip.Text = App.Text("Conflict.CherryPick");
            } else if (File.Exists(Path.Combine(repo.GitDir, "REBASE_HEAD"))) {
                txtConflictTip.Text = App.Text("Conflict.Rebase");
            } else if (File.Exists(Path.Combine(repo.GitDir, "REVERT_HEAD"))) {
                txtConflictTip.Text = App.Text("Conflict.Revert");
            } else if (File.Exists(Path.Combine(repo.GitDir, "MERGE_HEAD"))) {
                txtConflictTip.Text = App.Text("Conflict.Merge");
            } else {
                mergeNavigator.Visibility = Visibility.Collapsed;

                var rebaseTempFolder = Path.Combine(repo.GitDir, "rebase-apply");
                if (Directory.Exists(rebaseTempFolder)) Directory.Delete(rebaseTempFolder);

                var rebaseMergeFolder = Path.Combine(repo.GitDir, "rebase-merge");
                if (Directory.Exists(rebaseMergeFolder)) Directory.Delete(rebaseMergeFolder);
                return;
            }

            mergeNavigator.Visibility = Visibility.Visible;
            btnResolve.Visibility = workspace.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
            btnContinue.Visibility = changes.Find(x => x.IsConflit) == null ? Visibility.Visible : Visibility.Collapsed;

            (pages.Get("working_copy") as WorkingCopy).TryLoadMergeMessage();
        }

        private void GotoResolve(object sender, RoutedEventArgs e) {
            workspace.SelectedIndex = 1;
            e.Handled = true;
        }

        private async void ContinueMerge(object sender, RoutedEventArgs e) {
            var cherryPickMerge = Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD");
            var rebaseMerge = Path.Combine(repo.GitDir, "REBASE_HEAD");
            var revertMerge = Path.Combine(repo.GitDir, "REVERT_HEAD");
            var otherMerge = Path.Combine(repo.GitDir, "MERGE_HEAD");

            var mode = "";
            if (File.Exists(cherryPickMerge)) {
                mode = "cherry-pick";
            } else if (File.Exists(rebaseMerge)) {
                mode = "rebase";
            } else if (File.Exists(revertMerge)) {
                mode = "revert";
            } else if (File.Exists(otherMerge)) {
                mode = "merge";
            } else {
                UpdateWorkingCopy();
                return;
            }

            var cmd = new Commands.Command();
            cmd.Cwd = repo.Path;
            cmd.Args = $"-c core.editor=true {mode} --continue";

            Models.Watcher.SetEnabled(repo.Path, false);
            var succ = await Task.Run(() => cmd.Exec());
            Models.Watcher.SetEnabled(repo.Path, true);

            if (succ) {
                (pages.Get("working_copy") as WorkingCopy).ClearMessage();
                if (mode == "rebase") {
                    var rebaseTempFolder = Path.Combine(repo.GitDir, "rebase-apply");
                    if (Directory.Exists(rebaseTempFolder)) Directory.Delete(rebaseTempFolder);

                    var rebaseFile = Path.Combine(repo.GitDir, "REBASE_HEAD");
                    if (File.Exists(rebaseFile)) File.Delete(rebaseFile);

                    var rebaseMergeFolder = Path.Combine(repo.GitDir, "rebase-merge");
                    if (Directory.Exists(rebaseMergeFolder)) Directory.Delete(rebaseMergeFolder);
                }
            }
        }

        private async void AbortMerge(object sender, RoutedEventArgs e) {
            var cmd = new Commands.Command();
            cmd.Cwd = repo.Path;

            if (File.Exists(Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD"))) {
                cmd.Args = "cherry-pick --abort";
            } else if (File.Exists(Path.Combine(repo.GitDir, "REBASE_HEAD"))) {
                cmd.Args = "rebase --abort";
            } else if (File.Exists(Path.Combine(repo.GitDir, "REVERT_HEAD"))) {
                cmd.Args = "revert --abort";
            } else if (File.Exists(Path.Combine(repo.GitDir, "MERGE_HEAD"))) {
                cmd.Args = "merge --abort";
            } else {
                UpdateWorkingCopy();
                return;
            }

            Models.Watcher.SetEnabled(repo.Path, false);
            await Task.Run(() => cmd.Exec());
            Models.Watcher.SetEnabled(repo.Path, true);
            e.Handled = true;
        }
        #endregion
    }
}
