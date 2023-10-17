using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     工作区变更
    /// </summary>
    public partial class WorkingCopyChanges : UserControl {

        public static readonly DependencyProperty IsUnstagedProperty = DependencyProperty.Register(
            "IsUnstaged",
            typeof(bool),
            typeof(WorkingCopyChanges),
            new PropertyMetadata(false));

        public bool IsUnstaged {
            get { return (bool)GetValue(IsUnstagedProperty); }
            set { SetValue(IsUnstagedProperty, value); }
        }

        public static readonly DependencyProperty IsStagingProperty = DependencyProperty.Register(
            "IsStaging",
            typeof(bool),
            typeof(WorkingCopyChanges),
            new PropertyMetadata(false));

        public bool IsStaging {
            get { return (bool)GetValue(IsStagingProperty); }
            set { SetValue(IsStagingProperty, value); }
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
            "Mode",
            typeof(Models.Change.DisplayMode),
            typeof(WorkingCopyChanges),
            new PropertyMetadata(Models.Change.DisplayMode.Tree, OnModeChanged));

        public Models.Change.DisplayMode Mode {
            get { return (Models.Change.DisplayMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        public static readonly RoutedEvent DiffTargetChangedEvent = EventManager.RegisterRoutedEvent(
            "DiffTargetChanged",
            RoutingStrategy.Bubble,
            typeof(EventHandler<DiffTargetChangedEventArgs>),
            typeof(WorkingCopyChanges));

        public class DiffTargetChangedEventArgs : RoutedEventArgs {
            public Models.Change Target { get; set; }
            public bool HasOthers { get; set; }
            public DiffTargetChangedEventArgs(RoutedEvent re, object src, Models.Change c, bool hasOthers) : base(re, src) {
                Target = c;
                HasOthers = hasOthers;
            }
        }

        public event RoutedEventHandler DiffTargetChanged {
            add { AddHandler(DiffTargetChangedEvent, value); }
            remove { RemoveHandler(DiffTargetChangedEvent, value); }
        }

        public class ChangeNode {
            public string Path { get; set; } = "";
            public Models.Change Change { get; set; } = null;
            public bool IsExpanded { get; set; } = false;
            public bool IsFolder => Change == null;
            public ObservableCollection<ChangeNode> Children { get; set; } = new ObservableCollection<ChangeNode>();
        }

        public ObservableCollection<Models.Change> Changes {
            get;
            set;
        }

        public ObservableCollection<ChangeNode> Nodes {
            get;
            set;
        }

        public Models.Change DiffTarget {
            get;
            private set;
        }

        private string repo = null;
        private bool isLoadingData = false;

        public WorkingCopyChanges() {
            Changes = new ObservableCollection<Models.Change>();
            Nodes = new ObservableCollection<ChangeNode>();
            DiffTarget = null;

            InitializeComponent();
        }

        #region PUBLIC_METHODS
        public void SetRepository(string repo) {
            this.repo = repo;
        }

        public void UnselectAll() {
            switch (Mode) {
            case Models.Change.DisplayMode.Tree:
                modeTree.UnselectAll();
                break;
            case Models.Change.DisplayMode.List:
                modeList.SelectedItems.Clear();
                break;
            case Models.Change.DisplayMode.Grid:
                modeGrid.SelectedItems.Clear();
                break;
            }
        }

        public void StageSelected() {
            var changes = new List<Models.Change>();
            switch (Mode) {
            case Models.Change.DisplayMode.Tree:
                foreach (var node in modeTree.Selected) GetChangesFromNode(node as ChangeNode, changes);
                break;
            case Models.Change.DisplayMode.List:
                foreach (var c in modeList.SelectedItems) changes.Add(c as Models.Change);
                break;
            case Models.Change.DisplayMode.Grid:
                foreach (var c in modeGrid.SelectedItems) changes.Add(c as Models.Change);
                break;
            }

            var files = GetPathsFromChanges(changes);
            if (files.Count > 0) DoStage(files);
        }

        public void StageAll() {
            if (Models.Preference.Instance.Git.IncludeUntrackedInWC) {
                DoStage(null);
            } else {
                var changes = new List<string>();
                foreach (var c in Changes) changes.Add(c.Path);
                DoStage(changes);
            }
        }

        public void UnstageSelected() {
            var changes = new List<Models.Change>();
            switch (Mode) {
            case Models.Change.DisplayMode.Tree:
                foreach (var node in modeTree.Selected) GetChangesFromNode(node as ChangeNode, changes);
                break;
            case Models.Change.DisplayMode.List:
                foreach (var c in modeList.SelectedItems) changes.Add(c as Models.Change);
                break;
            case Models.Change.DisplayMode.Grid:
                foreach (var c in modeGrid.SelectedItems) changes.Add(c as Models.Change);
                break;
            }

            var files = GetPathsFromChanges(changes);
            if (files.Count > 0) DoUnstage(files);
        }

        public void UnstageAll() {
            DoUnstage(null);
        }

        public void SetData(List<Models.Change> changes) {
            isLoadingData = true;
            var isDefaultExpand = changes.Count <= 50;

            if (changes.Count == 0) {
                Changes.Clear();
                Nodes.Clear();
            } else if (Changes.Count == 0) {
                var nodesMap = new Dictionary<string, ChangeNode>();
                foreach (var c in changes) {
                    Changes.Add(c);

                    int sepIdx = c.Path.IndexOf("/", StringComparison.Ordinal);
                    if (sepIdx < 0) {
                        var n = AddTreeNode(Nodes, c.Path, c, false, true);
                        nodesMap.Add(c.Path, n);
                    } else {
                        ObservableCollection<ChangeNode> last = Nodes;
                        do {
                            ChangeNode parent = null;
                            var path = c.Path.Substring(0, sepIdx);
                            if (!nodesMap.TryGetValue(path, out parent)) {
                                parent = AddTreeNode(last, path, null, isDefaultExpand, true);
                                nodesMap.Add(path, parent);
                            }

                            last = parent.Children;
                            sepIdx = c.Path.IndexOf('/', sepIdx + 1);
                        } while (sepIdx > 0);

                        var n = AddTreeNode(last, c.Path, c, false, true);
                        nodesMap.Add(c.Path, n);
                    }
                }
                nodesMap.Clear();
            } else {
                var oldSet = new Dictionary<string, Models.Change>();
                var newSet = new Dictionary<string, Models.Change>();
                foreach (var c in changes) newSet.Add(c.Path, c);

                for (int i = Changes.Count - 1; i >= 0; i--) {
                    var old = Changes[i];
                    if (!newSet.ContainsKey(old.Path)) {
                        Changes.RemoveAt(i);
                        RemoveTreeNode(Nodes, old.Path);
                        if (modeTree.Selected.Contains(old)) modeTree.Selected.Remove(old);
                        if (DiffTarget == old) DiffTarget = null;
                        continue;
                    }

                    var cur = newSet[old.Path];
                    if (cur.Index != old.Index || cur.WorkTree != old.WorkTree) {
                        Changes.RemoveAt(i);
                        RemoveTreeNode(Nodes, old.Path);
                        if (modeTree.Selected.Contains(old)) modeTree.Selected.Remove(old);
                        if (DiffTarget == old) DiffTarget = null;
                        continue;
                    }

                    oldSet.Add(old.Path, old);
                }

                var nodesMap = new Dictionary<string, ChangeNode>();
                GetTreeNodes(nodesMap, Nodes);

                for (int i = 0; i < changes.Count; i++) {
                    var c = changes[i];
                    if (oldSet.ContainsKey(c.Path)) continue;

                    Changes.Insert(i, c);

                    int sepIdx = c.Path.IndexOf("/", StringComparison.Ordinal);
                    if (sepIdx < 0) {
                        var n = AddTreeNode(Nodes, c.Path, c, false, false);
                        nodesMap.Add(c.Path, n);
                    } else {
                        ObservableCollection<ChangeNode> last = Nodes;
                        do {
                            ChangeNode parent = null;
                            var path = c.Path.Substring(0, sepIdx);
                            if (!nodesMap.TryGetValue(path, out parent)) {
                                parent = AddTreeNode(last, path, null, isDefaultExpand, false);
                                nodesMap.Add(path, parent);
                            }

                            last = parent.Children;
                            sepIdx = c.Path.IndexOf('/', sepIdx + 1);
                        } while (sepIdx > 0);

                        var n = AddTreeNode(last, c.Path, c, false, false);
                        nodesMap.Add(c.Path, n);
                    }
                }
                nodesMap.Clear();
            }

            isLoadingData = false;
            return;
        }

        private void GetTreeNodes(Dictionary<string, ChangeNode> map, ObservableCollection<ChangeNode> nodes) {
            foreach (var n in nodes) {
                map.Add(n.Path, n);
                if (n.IsFolder) GetTreeNodes(map, n.Children);
            }
        }

        private ChangeNode AddTreeNode(ObservableCollection<ChangeNode> nodes, string path, Models.Change change, bool isExpand, bool isSorted = false) {
            var node = new ChangeNode();
            node.Path = path;
            node.Change = change;
            node.IsExpanded = isExpand;

            var added = false;
            if (change == null) {
                for (int i = 0; i < nodes.Count; i++) {
                    var n = nodes[i];
                    if (!n.IsFolder) {
                        added = true;
                        nodes.Insert(i, node);
                        break;
                    }

                    if (isSorted) continue;

                    if (n.Path.CompareTo(path) > 0) {
                        added = true;
                        nodes.Insert(i, node);
                        break;
                    }
                }
            } else {
                if (isSorted) {
                    added = true;
                    nodes.Add(node);
                } else {
                    for (int i = 0; i < nodes.Count; i++) {
                        var n = nodes[i];
                        if (n.IsFolder) continue;

                        if (n.Path.CompareTo(path) > 0) {
                            added = true;
                            nodes.Insert(i, node);
                            break;
                        }
                    }
                }
            }

            if (!added) nodes.Add(node);
            return node;
        }

        private bool RemoveTreeNode(ObservableCollection<ChangeNode> nodes, string path) {
            for (int i = nodes.Count - 1; i >= 0; i--) {
                var node = nodes[i];
                if (node.IsFolder) {
                    if (path.IndexOf(node.Path, StringComparison.Ordinal) < 0) {
                        continue;
                    }

                    if (RemoveTreeNode(node.Children, path)) {
                        if (node.Children.Count == 0) nodes.RemoveAt(i);
                        return true;
                    }
                } else {
                    if (path.Equals(node.Path, StringComparison.Ordinal)) {
                        nodes.RemoveAt(i);
                        return true;
                    }
                }
            }

            return false;
        }

        private void GetChangesFromNode(ChangeNode node, List<Models.Change> changes) {
            if (node.Change != null) {
                var idx = changes.FindIndex(x => x.Path == node.Change.Path);
                if (idx < 0) changes.Add(node.Change);
            } else {
                foreach (var sub in node.Children) GetChangesFromNode(sub, changes);
            }
        }

        private List<string> GetPathsFromChanges(List<Models.Change> changes) {
            var files = new List<string>();
            foreach (var c in changes) {
                files.Add(c.Path);
                if (!string.IsNullOrEmpty(c.OriginalPath)) files.Add(c.OriginalPath);
            }
            return files;
        }
        #endregion

        #region UNSTAGED
        private async void DoStage(List<string> files) {
            IsStaging = true;
            Models.Watcher.SetEnabled(repo, false);
            if (files == null || files.Count == 0) {
                await Task.Run(() => new Commands.Add(repo).Exec());
            } else {
                for (int i = 0; i < files.Count; i += 10) {
                    var maxCount = Math.Min(10, files.Count - i);
                    var step = files.GetRange(i, maxCount);
                    await Task.Run(() => new Commands.Add(repo, step).Exec());
                }
            }
            Models.Watcher.SetEnabled(repo, true);
            Models.Watcher.Get(repo)?.RefreshWC();
            IsStaging = false;
        }

        private void Disard(List<Models.Change> changes) {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null) {
                if (parent is WorkingCopy wc) {
                    wc.Discard(changes);
                    return;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        private async void SaveAsPatch(string saveTo, List<Models.Change> changes) {
            var stream = new FileStream(saveTo, FileMode.Create);
            var writer = new StreamWriter(stream);

            foreach (var c in changes) {
                await Task.Run(() => new Commands.SaveChangeToStream(repo, c, writer).Exec());
            }

            writer.Flush();
            stream.Flush();
            writer.Close();
            stream.Close();

            new ConfirmDialog(App.Text("Notice"), App.Text("SaveAsPatchSuccess")).ShowDialog();
        }

        private void OpenUnstagedContextMenuByNodes(ContextMenu menu, List<ChangeNode> nodes, List<Models.Change> changes) {
            var files = new List<string>();
            foreach (var c in changes) files.Add(c.Path);

            if (nodes.Count == 1) {
                var node = nodes[0];
                var path = Path.GetFullPath(Path.Combine(repo, node.Path));

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, e) => {
                    if (node.IsFolder) Process.Start("explorer", path);
                    else Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };

                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.Stage");
                stage.Click += (o, e) => {
                    DoStage(files);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.Discard");
                discard.Click += (o, e) => {
                    Disard(changes);
                    e.Handled = true;
                };

                var stashIcon = new System.Windows.Shapes.Path();
                stashIcon.Data = FindResource("Icon.Stashes") as System.Windows.Media.Geometry;
                stashIcon.Width = 10;
                var stash = new MenuItem();
                stash.Icon = stashIcon;
                stash.Header = App.Text("FileCM.Stash");
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, changes).Show();
                    e.Handled = true;
                };

                var patchIcon = new System.Windows.Shapes.Path();
                patchIcon.Data = FindResource("Icon.Diff") as System.Windows.Media.Geometry;
                patchIcon.Width = 10;
                var patch = new MenuItem();
                patch.Icon = patchIcon;
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = repo;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatch(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetDataObject(node.Path, true);
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(new Separator());
                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new Separator());
                if (node.Change != null) {
                    var assumeUnchanged = new MenuItem();
                    assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                    assumeUnchanged.Click += (o, e) => {
                        new Commands.AssumeUnchanged(repo).Add(node.Path);
                        e.Handled = true;
                    };
                    menu.Items.Add(assumeUnchanged);

                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Click += (o, e) => {
                        var viewer = new FileHistories(repo, node.Path);
                        viewer.Show();
                        e.Handled = true;
                    };
                    menu.Items.Add(history);
                    menu.Items.Add(new Separator());
                }
                menu.Items.Add(copyPath);
            } else {
                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.StageMulti", changes.Count);
                stage.Click += (o, e) => {
                    DoStage(files);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", changes.Count);
                discard.Click += (o, e) => {
                    Disard(changes);
                    e.Handled = true;
                };

                var stashIcon = new System.Windows.Shapes.Path();
                stashIcon.Data = FindResource("Icon.Stashes") as System.Windows.Media.Geometry;
                stashIcon.Width = 10;
                var stash = new MenuItem();
                stash.Icon = stashIcon;
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, changes).Show();
                    e.Handled = true;
                };

                var patchIcon = new System.Windows.Shapes.Path();
                patchIcon.Data = FindResource("Icon.Diff") as System.Windows.Media.Geometry;
                patchIcon.Width = 10;
                var patch = new MenuItem();
                patch.Icon = patchIcon;
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = repo;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatch(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
            }
        }

        private void OpenUnstagedContextMenuByChanges(ContextMenu menu, List<Models.Change> changes) {
            var files = new List<string>();
            foreach (var c in changes) files.Add(c.Path);

            if (changes.Count == 1) {
                var change = changes[0];
                var path = Path.GetFullPath(Path.Combine(repo, change.Path));

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
                    DoStage(files);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.Discard");
                discard.Click += (o, e) => {
                    Disard(changes);
                    e.Handled = true;
                };

                var stashIcon = new System.Windows.Shapes.Path();
                stashIcon.Data = FindResource("Icon.Stashes") as System.Windows.Media.Geometry;
                stashIcon.Width = 10;
                var stash = new MenuItem();
                stash.Icon = stashIcon;
                stash.Header = App.Text("FileCM.Stash");
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, changes).Show();
                    e.Handled = true;
                };

                var patchIcon = new System.Windows.Shapes.Path();
                patchIcon.Data = FindResource("Icon.Diff") as System.Windows.Media.Geometry;
                patchIcon.Width = 10;
                var patch = new MenuItem();
                patch.Icon = patchIcon;
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = repo;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatch(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetDataObject(change.Path, true);
                    e.Handled = true;
                };

                var history = new MenuItem();
                history.Header = App.Text("FileHistory");
                history.Click += (o, e) => {
                    var viewer = new FileHistories(repo, change.Path);
                    viewer.Show();
                    e.Handled = true;
                };

                var assumeUnchanged = new MenuItem();
                assumeUnchanged.Header = App.Text("FileCM.AssumeUnchanged");
                assumeUnchanged.Click += (o, e) => {
                    new Commands.AssumeUnchanged(repo).Add(change.Path);
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(new Separator());
                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
                menu.Items.Add(new Separator());
                menu.Items.Add(assumeUnchanged);
                menu.Items.Add(history);
                menu.Items.Add(new Separator());
                menu.Items.Add(copyPath);
            } else {
                var stage = new MenuItem();
                stage.Header = App.Text("FileCM.StageMulti", changes.Count);
                stage.Click += (o, e) => {
                    DoStage(files);
                    e.Handled = true;
                };

                var discard = new MenuItem();
                discard.Header = App.Text("FileCM.DiscardMulti", changes.Count);
                discard.Click += (o, e) => {
                    Disard(changes);
                    e.Handled = true;
                };

                var stashIcon = new System.Windows.Shapes.Path();
                stashIcon.Data = FindResource("Icon.Stashes") as System.Windows.Media.Geometry;
                stashIcon.Width = 10;
                var stash = new MenuItem();
                stash.Icon = stashIcon;
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, changes).Show();
                    e.Handled = true;
                };

                var patchIcon = new System.Windows.Shapes.Path();
                patchIcon.Data = FindResource("Icon.Diff") as System.Windows.Media.Geometry;
                patchIcon.Width = 10;
                var patch = new MenuItem();
                patch.Icon = patchIcon;
                patch.Header = App.Text("FileCM.SaveAsPatch");
                patch.Click += (o, e) => {
                    var dialog = new SaveFileDialog();
                    dialog.Filter = "Patch File|*.patch";
                    dialog.Title = App.Text("FileCM.SaveAsPatch");
                    dialog.InitialDirectory = repo;

                    if (dialog.ShowDialog() == true) {
                        SaveAsPatch(dialog.FileName, changes);
                    }

                    e.Handled = true;
                };

                menu.Items.Add(stage);
                menu.Items.Add(discard);
                menu.Items.Add(stash);
                menu.Items.Add(patch);
            }
        }
        #endregion

        #region STAGED
        private async void DoUnstage(List<string> files) {
            Models.Watcher.SetEnabled(repo, false);
            if (files == null || files.Count == 0) {
                await Task.Run(() => new Commands.Reset(repo).Exec());
            } else {
                for (int i = 0; i < files.Count; i += 10) {
                    var maxCount = Math.Min(10, files.Count - i);
                    var step = files.GetRange(i, maxCount);
                    await Task.Run(() => new Commands.Reset(repo, step).Exec());
                }
            }
            Models.Watcher.SetEnabled(repo, true);
            Models.Watcher.Get(repo)?.RefreshWC();
        }

        private void OpenStagedContextMenuByNodes(ContextMenu menu, List<ChangeNode> nodes, List<Models.Change> changes) {
            var files = GetPathsFromChanges(changes);

            if (nodes.Count == 1) {
                var node = nodes[0];
                var path = Path.GetFullPath(Path.Combine(repo, node.Path));

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, e) => {
                    if (node.IsFolder) Process.Start(path);
                    else Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Click += (o, e) => {
                    DoUnstage(files);
                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetDataObject(node.Path, true);
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(unstage);
                menu.Items.Add(copyPath);
            } else {
                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", files.Count);
                unstage.Click += (o, e) => {
                    DoUnstage(files);
                    e.Handled = true;
                };

                menu.Items.Add(unstage);
            }
        }

        private void OpenStagedContextMenuByChanges(ContextMenu menu, List<Models.Change> changes) {
            var files = GetPathsFromChanges(changes);

            if (changes.Count == 1) {
                var change = changes[0];
                var path = Path.GetFullPath(Path.Combine(repo, change.Path));

                var explore = new MenuItem();
                explore.IsEnabled = File.Exists(path) || Directory.Exists(path);
                explore.Header = App.Text("RevealFile");
                explore.Click += (o, e) => {
                    Process.Start("explorer", $"/select,{path}");
                    e.Handled = true;
                };

                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.Unstage");
                unstage.Click += (o, e) => {
                    DoUnstage(files);
                    e.Handled = true;
                };

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("CopyPath");
                copyPath.Click += (o, e) => {
                    Clipboard.SetDataObject(change.Path, true);
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(new Separator());
                menu.Items.Add(unstage);
                menu.Items.Add(new Separator());
                menu.Items.Add(copyPath);
            } else {
                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", files.Count);
                unstage.Click += (o, e) => {
                    DoUnstage(files);
                    e.Handled = true;
                };

                menu.Items.Add(unstage);
            }
        }
        #endregion

        #region EVENTS
        private void OnChangePreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None) {
                if (!IsUnstaged) {
                    UnstageSelected();
                } else {
                    StageSelected();
                }

                e.Handled = true;
            }
        }

        private void OnTreeSelectionChanged(object sender, RoutedEventArgs e) {
            if (Mode != Models.Change.DisplayMode.Tree) return;

            bool hasOthers = false;
            if (modeTree.Selected.Count == 0) {
                DiffTarget = null;
            } else if (modeTree.Selected.Count == 1) {
                var node = modeTree.Selected[0] as ChangeNode;
                if (node.IsFolder) {
                    DiffTarget = null;
                    hasOthers = true;
                } else {
                    DiffTarget = node.Change;
                }
            } else {
                if (DiffTarget == null) return;
                DiffTarget = null;
                hasOthers = true;
            }

            if (!isLoadingData) RaiseEvent(new DiffTargetChangedEventArgs(DiffTargetChangedEvent, this, DiffTarget, hasOthers));
        }

        private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Mode != Models.Change.DisplayMode.List) return;

            bool hasOthers = false;
            switch (modeList.SelectedItems.Count) {
            case 0:
                DiffTarget = null;
                break;
            case 1:
                DiffTarget = modeList.SelectedItems[0] as Models.Change;
                break;
            default:
                DiffTarget = null;
                hasOthers = true;
                break;
            }

            if (!isLoadingData) RaiseEvent(new DiffTargetChangedEventArgs(DiffTargetChangedEvent, this, DiffTarget, hasOthers));
        }

        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Mode != Models.Change.DisplayMode.Grid) return;

            bool hasOthers = false;
            switch (modeGrid.SelectedItems.Count) {
            case 0:
                DiffTarget = null;
                break;
            case 1:
                DiffTarget = modeGrid.SelectedItems[0] as Models.Change;
                break;
            default:
                DiffTarget = null;
                hasOthers = true;
                break;
            }

            if (!isLoadingData) RaiseEvent(new DiffTargetChangedEventArgs(DiffTargetChangedEvent, this, DiffTarget, hasOthers));
        }

        private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var nodes = new List<ChangeNode>();
            var changes = new List<Models.Change>();

            foreach (var o in modeTree.Selected) {
                nodes.Add(o as ChangeNode);
                GetChangesFromNode(o as ChangeNode, changes);
            }

            var menu = new ContextMenu() { PlacementTarget = sender as UIElement };
            if (IsUnstaged) {
                OpenUnstagedContextMenuByNodes(menu, nodes, changes);
            } else {
                OpenStagedContextMenuByNodes(menu, nodes, changes);
            }

            menu.IsOpen = true;
            ev.Handled = true;
        }

        private void OnDataGridContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var row = sender as DataGridRow;
            if (row == null) return;

            var changes = new List<Models.Change>();
            if (Mode == Models.Change.DisplayMode.List) {
                if (!row.IsSelected) {
                    modeList.SelectedItem = row.DataContext;
                    changes.Add(row.DataContext as Models.Change);
                } else {
                    foreach (var c in modeList.SelectedItems) changes.Add(c as Models.Change);
                }
            } else {
                if (!row.IsSelected) {
                    modeGrid.SelectedItem = row.DataContext;
                    changes.Add(row.DataContext as Models.Change);
                } else {
                    foreach (var c in modeGrid.SelectedItems) changes.Add(c as Models.Change);
                }
            }

            var menu = new ContextMenu() { PlacementTarget = row };
            if (IsUnstaged) {
                OpenUnstagedContextMenuByChanges(menu, changes);
            } else {
                OpenStagedContextMenuByChanges(menu, changes);
            }

            menu.IsOpen = true;
            ev.Handled = true;
        }

        private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        private void OnListSizeChanged(object sender, SizeChangedEventArgs e) {
            if (Mode != Models.Change.DisplayMode.List) return;

            int last = modeList.Columns.Count - 1;
            double offset = 0;
            for (int i = 0; i < last; i++) offset += modeList.Columns[i].ActualWidth;
            modeList.Columns[last].MinWidth = Math.Max(modeList.ActualWidth - offset, 10);
            modeList.UpdateLayout();
        }

        private void OnGridSizeChanged(object sender, SizeChangedEventArgs e) {
            if (Mode != Models.Change.DisplayMode.Grid) return;

            int last = modeGrid.Columns.Count - 1;
            double offset = 0;
            for (int i = 0; i < last; i++) offset += modeGrid.Columns[i].ActualWidth;
            modeGrid.Columns[last].MinWidth = Math.Max(modeGrid.ActualWidth - offset, 10);
            modeGrid.UpdateLayout();
        }

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var elem = d as WorkingCopyChanges;
            if (elem != null) {
                if (elem.modeTree != null) {
                    if (elem.Mode == Models.Change.DisplayMode.Tree) {
                        elem.modeTree.Visibility = Visibility.Visible;
                    } else {
                        elem.modeTree.Visibility = Visibility.Collapsed;
                    }
                }

                if (elem.modeList != null) {
                    if (elem.Mode == Models.Change.DisplayMode.List) {
                        elem.modeList.Visibility = Visibility.Visible;
                    } else {
                        elem.modeList.Visibility = Visibility.Collapsed;
                    }
                }

                if (elem.modeGrid != null) {
                    if (elem.Mode == Models.Change.DisplayMode.Grid) {
                        elem.modeGrid.Visibility = Visibility.Visible;
                    } else {
                        elem.modeGrid.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        #endregion
    }
}
