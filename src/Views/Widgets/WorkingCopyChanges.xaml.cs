using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            var files = new List<string>();
            switch (Mode) {
            case Models.Change.DisplayMode.Tree:
                var changes = new List<Models.Change>();
                foreach (var node in modeTree.Selected) GetChangesFromNode(node as ChangeNode, changes);
                foreach (var c in changes) files.Add(c.Path);
                break;
            case Models.Change.DisplayMode.List:
                foreach (var c in modeList.SelectedItems) files.Add((c as Models.Change).Path);
                break;
            case Models.Change.DisplayMode.Grid:
                foreach (var c in modeGrid.SelectedItems) files.Add((c as Models.Change).Path);
                break;
            }
            if (files.Count > 0) DoStage(files);
        }

        public void StageAll() {
            DoStage(null);
        }

        public void UnstageSelected() {
            var files = new List<string>();
            switch (Mode) {
            case Models.Change.DisplayMode.Tree:
                var changes = new List<Models.Change>();
                foreach (var node in modeTree.Selected) GetChangesFromNode(node as ChangeNode, changes);
                foreach (var c in changes) files.Add(c.Path);
                break;
            case Models.Change.DisplayMode.List:
                foreach (var c in modeList.SelectedItems) files.Add((c as Models.Change).Path);
                break;
            case Models.Change.DisplayMode.Grid:
                foreach (var c in modeGrid.SelectedItems) files.Add((c as Models.Change).Path);
                break;
            }
            if (files.Count > 0) DoUnstage(files);
        }

        public void UnstageAll() {
            DoUnstage(null);
        }

        public void SetData(List<Models.Change> changes) {
            isLoadingData = true;

            var oldSet = new Dictionary<string, Models.Change>();
            var newSet = new Dictionary<string, Models.Change>();
            foreach (var c in changes) newSet.Add(c.Path, c);

            for (int i = Changes.Count - 1; i >= 0; i--) {
                var old = Changes[i];
                if (!newSet.ContainsKey(old.Path)) {
                    Changes.RemoveAt(i);
                    RemoveTreeNode(Nodes, old);
                    continue;
                }

                var cur = newSet[old.Path];
                if (cur.Index != old.Index || cur.WorkTree != old.WorkTree) {
                    Changes.RemoveAt(i);
                    RemoveTreeNode(Nodes, old);
                    continue;
                }

                oldSet.Add(old.Path, old);
            }

            var isDefaultExpand = changes.Count <= 50;
            foreach (var c in changes) {
                if (oldSet.ContainsKey(c.Path)) continue;

                bool added = false;
                for (int i = 0; i < Changes.Count; i++) {
                    if (c.Path.CompareTo(Changes[i].Path) < 0) {
                        Changes.Insert(i, c);
                        added = true;
                        break;
                    }
                }

                if (!added) Changes.Add(c);

#if NET48
                int sepIdx = c.Path.IndexOf("/", StringComparison.Ordinal);
#else
                int sepIdx = c.Path.IndexOf('/', StringComparison.Ordinal);
#endif
                if (sepIdx < 0) {
                    GetOrAddTreeNode(Nodes, c.Path, c, false);
                } else {
                    ObservableCollection<ChangeNode> last = Nodes;
                    do {
                        var path = c.Path.Substring(0, sepIdx);
                        last = GetOrAddTreeNode(last, path, null, isDefaultExpand).Children;
                        sepIdx = c.Path.IndexOf('/', sepIdx + 1);
                    } while (sepIdx > 0);
                    GetOrAddTreeNode(last, c.Path, c, false);
                }
            }

            isLoadingData = false;
        }

        private ChangeNode GetOrAddTreeNode(ObservableCollection<ChangeNode> nodes, string path, Models.Change change, bool isExpand) {
            foreach (var n in nodes) {
                if (n.Path == path) return n;
            }

            var node = new ChangeNode();
            node.Path = path;
            node.Change = change;
            node.IsExpanded = isExpand;

            var added = false;
            if (change == null) {
                for (int i = 0; i < nodes.Count; i++) {
                    if (!nodes[i].IsFolder || nodes[i].Path.CompareTo(path) > 0) {
                        added = true;
                        nodes.Add(node);
                        break;
                    }
                }
            } else {
                for (int i = 0; i < nodes.Count; i++) {
                    if (nodes[i].IsFolder) continue;
                    if (nodes[i].Path.CompareTo(path) > 0) {
                        added = true;
                        nodes.Add(node);
                        break;
                    }
                }
            }

            if (!added) nodes.Add(node);
            return node;
        }

        private bool RemoveTreeNode(ObservableCollection<ChangeNode> nodes, Models.Change change) {
            for (int i = nodes.Count - 1; i >= 0; i--) {
                var node = nodes[i];
                if (node.Change == null) {
                    if (RemoveTreeNode(node.Children, change)) {
                        if (node.Children.Count == 0) nodes.RemoveAt(i);
                        return true;
                    }
                } else if (node.Change.Path == change.Path) {
                    nodes.RemoveAt(i);
                    return true;
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

        private async void SaveAsPatch(string saveTo, List<Models.Change> changes) {
            FileStream stream = new FileStream(saveTo, FileMode.Create);
            StreamWriter writer = new StreamWriter(stream);

            foreach (var c in changes) {
                await Task.Run(() => new Commands.SaveChangeToStream(repo, c, writer).Exec());
            }

            writer.Flush();
            stream.Flush();
            writer.Close();
            stream.Close();
        }

        private void OpenUnstagedContextMenuByNodes(List<ChangeNode> nodes, List<Models.Change> changes) {
            var files = new List<string>();
            foreach (var c in changes) files.Add(c.Path);

            var menu = new ContextMenu();
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
                    new Popups.Discard(repo, changes).Show();
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, files).Show();
                    e.Handled = true;
                };

                var patch = new MenuItem();
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
                    Clipboard.SetText(node.Path);
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
                    var history = new MenuItem();
                    history.Header = App.Text("FileHistory");
                    history.Click += (o, e) => {
                        var viewer = new Views.Histories(repo, node.Path);
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
                    new Popups.Discard(repo, changes).Show();
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, files).Show();
                    e.Handled = true;
                };

                var patch = new MenuItem();
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

            menu.IsOpen = true;
        }

        private void OpenUnstagedContextMenuByChanges(List<Models.Change> changes) {
            var files = new List<string>();
            foreach (var c in changes) files.Add(c.Path);

            var menu = new ContextMenu();
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
                    new Popups.Discard(repo, changes).Show();
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.Stash");
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, files).Show();
                    e.Handled = true;
                };

                var patch = new MenuItem();
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
                    Clipboard.SetText(change.Path);
                    e.Handled = true;
                };

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
                        var viewer = new Views.Histories(repo, change.Path);
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
                    new Popups.Discard(repo, changes).Show();
                    e.Handled = true;
                };

                var stash = new MenuItem();
                stash.Header = App.Text("FileCM.StashMulti", changes.Count);
                stash.Click += (o, e) => {
                    new Popups.Stash(repo, files).Show();
                    e.Handled = true;
                };

                var patch = new MenuItem();
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

            menu.IsOpen = true;
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

        private void OpenStagedContextMenuByNodes(List<ChangeNode> nodes, List<Models.Change> changes) {
            var files = new List<string>();
            foreach (var c in changes) files.Add(c.Path);

            var menu = new ContextMenu();
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
                    Clipboard.SetText(node.Path);
                    e.Handled = true;
                };
            } else {
                var unstage = new MenuItem();
                unstage.Header = App.Text("FileCM.UnstageMulti", files.Count);
                unstage.Click += (o, e) => {
                    DoUnstage(files);
                    e.Handled = true;
                };

                menu.Items.Add(unstage);
            }

            menu.IsOpen = true;
        }

        private void OpenStagedContextMenuByChanges(List<Models.Change> changes) {
            var files = new List<string>();
            foreach (var c in changes) files.Add(c.Path);

            var menu = new ContextMenu();
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
                    Clipboard.SetText(change.Path);
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
            menu.IsOpen = true;
        }
        #endregion

        #region EVENTS
        private void SelectWholeTree(object sender, ExecutedRoutedEventArgs e) {
            modeTree.SelectAll();
        }

        private void OnTreeSelectionChanged(object sender, RoutedEventArgs e) {
            if (Mode != Models.Change.DisplayMode.Tree) return;

            bool hasOthers = false;
            if (modeTree.Selected.Count == 0) {
                DiffTarget = null;
            } else if (modeTree.Selected.Count == 1) {
                var node = modeTree.Selected[0] as ChangeNode;
                if (node.IsFolder) {
                    if (DiffTarget == null) return;
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

            if (IsUnstaged) {
                OpenUnstagedContextMenuByNodes(nodes, changes);
            } else {
                OpenStagedContextMenuByNodes(nodes, changes);
            }

            ev.Handled = true;
        }

        private void OnDataGridContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var row = sender as DataGridRow;
            if (row == null) return;

            var changes = new List<Models.Change>();
            if (Mode == Models.Change.DisplayMode.List) {
                if (!row.IsSelected) {
                    modeList.SelectedItems.Clear();
                    modeList.SelectedItems.Add(row.DataContext);
                    changes.Add(row.DataContext as Models.Change);
                } else {
                    foreach (var c in modeList.SelectedItems) changes.Add(c as Models.Change);
                }
            } else {
                if (!row.IsSelected) {
                    modeGrid.SelectedItems.Clear();
                    modeGrid.SelectedItems.Add(row.DataContext);
                    changes.Add(row.DataContext as Models.Change);
                } else {
                    foreach (var c in modeGrid.SelectedItems) changes.Add(c as Models.Change);
                }
            }

            if (IsUnstaged) {
                OpenUnstagedContextMenuByChanges(changes);
            } else {
                OpenStagedContextMenuByChanges(changes);
            }

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
